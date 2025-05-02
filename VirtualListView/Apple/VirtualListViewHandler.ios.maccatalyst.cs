#nullable enable
using CoreGraphics;
using Foundation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace Microsoft.Maui;

public partial class VirtualListViewHandler : ViewHandler<IVirtualListView, UIView>
{
	CvDataSource? dataSource;
	CvLayout? layout;
	UIRefreshControl? refreshControl;
	
	protected virtual VirtualListViewController CreateController() => new(this);

	protected override UIView CreatePlatformView()
	{
		Controller = this.CreateController();

		var collectionView = this.Controller.CollectionView;
		collectionView.AllowsMultipleSelection = false;
		collectionView.AllowsSelection = false;
		this.layout = (CvLayout)collectionView.CollectionViewLayout;

		refreshControl = new UIRefreshControl();
		refreshControl.Enabled = VirtualView?.IsRefreshEnabled ?? false;
		refreshControl.AddTarget(new EventHandler((s, a) =>
		{
			refreshControl.BeginRefreshing();
			try
			{
				VirtualView?.Refresh(() => refreshControl.EndRefreshing());
			}
			catch
			{
				refreshControl.EndRefreshing();
			}
		}), UIControlEvent.ValueChanged);
		
		return collectionView;
	}

    public VirtualListViewController Controller { get; private set; }

    protected override void ConnectHandler(UIView nativeView)
	{
		base.ConnectHandler(nativeView);

		var collectionView = nativeView as UICollectionView;

        PositionalViewSelector = new PositionalViewSelector(VirtualView);
		
		collectionView.ReloadData();
	}

	protected override void DisconnectHandler(UIView nativeView)
	{
		if (dataSource is not null)
		{
			dataSource.Dispose();
			dataSource = null;
		}

		if (Controller is not null)
		{
			Controller.Dispose();
			Controller = null;
		}

		if (refreshControl is not null)
		{
			refreshControl.RemoveFromSuperview();
			refreshControl.Dispose();
			refreshControl = null;
		}


		nativeView.Dispose();

		if (layout is not null)
		{
			layout.Dispose();
			layout = null;
		}

		base.DisconnectHandler(nativeView);
	}

	internal CvCell? GetCell(NSIndexPath indexPath)
		=> dataSource?.GetCell(PlatformView as UICollectionView, indexPath) as CvCell;

	public static void MapHeader(VirtualListViewHandler handler, IVirtualListView virtualListView)
		=> handler?.InvalidateData();

	public static void MapFooter(VirtualListViewHandler handler, IVirtualListView virtualListView)
		=> handler?.InvalidateData();

	public static void MapViewSelector(VirtualListViewHandler handler, IVirtualListView virtualListView)
		=> handler?.InvalidateData();

	public static void MapSelectionMode(VirtualListViewHandler handler, IVirtualListView virtualListView)
	{
	}

	public static void MapInvalidateData(VirtualListViewHandler handler, IVirtualListView virtualListView, object? parameter)
		=> handler?.InvalidateData();

	void PlatformScrollToItem(ItemPosition itemPosition, bool animated)
	{
		var realIndex = PositionalViewSelector?.GetPosition(itemPosition.SectionIndex, itemPosition.ItemIndex) ?? -1;

		if (realIndex < 0)
			return;

		var indexPath = NSIndexPath.FromItemSection(realIndex, 0);

		((UICollectionView)PlatformView).ScrollToItem(indexPath, UICollectionViewScrollPosition.Top, animated);
	}

	void PlatformUpdateItemSelection(ItemPosition itemPosition, bool selected)
	{
		var realIndex = PositionalViewSelector?.GetPosition(itemPosition.SectionIndex, itemPosition.ItemIndex) ?? -1;

		if (realIndex < 0)
			return;

		var cell = ((UICollectionView)PlatformView).CellForItem(NSIndexPath.FromItemSection(realIndex, 0));

		if (cell is CvCell cvcell)
		{
			PlatformView.InvokeOnMainThread(() =>
			{
				cvcell.UpdateSelected(selected);
			});
		}
	}

	public static void MapOrientation(VirtualListViewHandler handler, IVirtualListView virtualListView)
	{
		if (handler.layout is not null)
		{
			if (virtualListView.Orientation == ListOrientation.Vertical)
			{
				handler.layout.ScrollDirection = UICollectionViewScrollDirection.Vertical;
				handler.Controller.CollectionView.AlwaysBounceVertical = true;
				handler.Controller.CollectionView.AlwaysBounceHorizontal = false;
			}
			else
			{
				handler.layout.ScrollDirection = UICollectionViewScrollDirection.Horizontal;
				handler.Controller.CollectionView.AlwaysBounceVertical = false;
				handler.Controller.CollectionView.AlwaysBounceHorizontal = true;
			}
		}

		handler.InvalidateData();
	}

	public static void MapRefreshAccentColor(VirtualListViewHandler handler, IVirtualListView virtualListView)
	{
		if (virtualListView.RefreshAccentColor is not null && handler.refreshControl is not null)
			handler.refreshControl.TintColor = virtualListView.RefreshAccentColor.ToPlatform();
	}

	public static void MapIsRefreshEnabled(VirtualListViewHandler handler, IVirtualListView virtualListView)
	{
		var isRefreshEnabled = virtualListView?.IsRefreshEnabled ?? false;
		if (handler.refreshControl is not null)
		{
			if (isRefreshEnabled)
			{
				handler.PlatformView.AddSubview(handler.refreshControl);
				handler.refreshControl.Enabled = true;
			}
			else
			{
				handler.refreshControl.Enabled = false;
				handler.refreshControl.RemoveFromSuperview();
			}
		}
	}


	public static void MapEmptyView(VirtualListViewHandler handler, IVirtualListView virtualListView)
	{
		handler?.UpdateEmptyView();
	}

	void UpdateEmptyViewVisibility()
	{
		if (PlatformView is not null && ((UICollectionView)PlatformView).BackgroundView is not null)
		{
			var visibility = ShouldShowEmptyView ? Visibility.Visible : Visibility.Collapsed;

            ((UICollectionView)PlatformView).BackgroundView?.UpdateVisibility(visibility);
		}
	}

	void UpdateEmptyView()
	{
		if (PlatformView is not null)
		{
			if (((UICollectionView)PlatformView).BackgroundView is not null)
			{
                ((UICollectionView)PlatformView).BackgroundView.RemoveFromSuperview();
                ((UICollectionView)PlatformView).BackgroundView.Dispose();
			}

			if (MauiContext is not null)
				((UICollectionView)PlatformView).BackgroundView = VirtualView?.EmptyView?.ToPlatform(MauiContext);

			UpdateEmptyViewVisibility();
		}
	}

	void UpdateVerticalScrollbarVisibility(ScrollBarVisibility scrollBarVisibility)
	{
		((UICollectionView)PlatformView).ShowsVerticalScrollIndicator = scrollBarVisibility == ScrollBarVisibility.Always || scrollBarVisibility == ScrollBarVisibility.Default;
	}
	
	void UpdateHorizontalScrollbarVisibility(ScrollBarVisibility scrollBarVisibility)
	{
        ((UICollectionView)PlatformView).ShowsHorizontalScrollIndicator = scrollBarVisibility == ScrollBarVisibility.Always || scrollBarVisibility == ScrollBarVisibility.Default;
	}

	protected virtual bool SuspendInvalidateUpdate() => false;

	public void InvalidateData()
	{
		if (this.SuspendInvalidateUpdate() || this.Controller is null)
		{
			return;
		}

		this.PlatformView.InvokeOnMainThread(() => {
            var (originalContentHeight, originalScrollOffset) = this.GetScrollPositionInfo();

			UpdateEmptyViewVisibility();

			Controller.DataSource?.ReloadData();
            ((UICollectionView)PlatformView)?.ReloadData();
			
            // Adjust the scroll position
            var (finalContentHeight, finalScrollOffset) = this.GetScrollPositionInfo();
            if (originalScrollOffset != finalScrollOffset)
            {
                var delta = originalContentHeight - finalContentHeight;
				((UICollectionView)PlatformView).SetContentOffset(new CGPoint(0, finalScrollOffset + delta), false);
            }
		});
		
	}

	private (nfloat contentHeight, nfloat scrollOffset)  GetScrollPositionInfo()
	{
		return (((UICollectionView)PlatformView).ContentSize.Height, ((UICollectionView)PlatformView).ContentOffset.Y);
	}
	
	public IReadOnlyList<IPositionInfo> FindVisiblePositions()
	{
		var positions = new List<PositionInfo>();
			
		foreach (var cell in ((UICollectionView)PlatformView).VisibleCells)
		{
			if (cell is CvCell cvCell)
				positions.Add(cvCell.PositionInfo);
		}

		return positions;
	}
}
