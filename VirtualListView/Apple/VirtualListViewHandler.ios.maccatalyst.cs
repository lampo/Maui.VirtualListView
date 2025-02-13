#nullable enable
using Foundation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.HotReload;
using Microsoft.Maui.Platform;
using UIKit;

namespace Microsoft.Maui;

public partial class VirtualListViewHandler : ViewHandler<IVirtualListView, UIView>
{
	CvDataSource? dataSource;
	CvLayout? layout;
	UIRefreshControl? refreshControl;

	protected override UIView CreatePlatformView()
	{
		Controller = new VirtualListViewController(this);

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

		//collectionView.AddSubview(refreshControl);
		//collectionView.ContentInset = new UIEdgeInsets(0, 0, 0, 0);
		//collectionView.ScrollIndicatorInsets = new UIEdgeInsets(0, 0, 0, 0);
		//collectionView.AutomaticallyAdjustsScrollIndicatorInsets = false;
		
		return collectionView;
	}

    public VirtualListViewController Controller { get; private set; }

    protected override void ConnectHandler(UIView nativeView)
	{
		base.ConnectHandler(nativeView);

		var collectionView = nativeView as UICollectionView;

        PositionalViewSelector = new PositionalViewSelector(VirtualView);

		//dataSource = new CvDataSource(this);
		
		//cvdelegate = new CvDelegate(this, collectionView);
		// ((CvDelegate)collectionView.Delegate).ScrollHandler = (x, y) => VirtualView?.Scrolled(x, y);

		//collectionView.DataSource = dataSource;
		//collectionView.Delegate = cvdelegate;
		
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

	public void InvalidateData()
	{
		if (Controller.DataSource.SuspendReload)
		{
			return;
		}

		this.PlatformView.InvokeOnMainThread(() => {
			//layout?.InvalidateLayout();
            var originalFirstVisibleItemIndexPath = ((UICollectionView)PlatformView).IndexPathsForVisibleItems.OrderBy(ip => ip.Row).FirstOrDefault();
            var positionDelta = GetCurrentScrollPositionDelta();

			UpdateEmptyViewVisibility();

			//PlatformView?.SetNeedsLayout();
			Controller.DataSource?.ReloadData();
            ((UICollectionView)PlatformView)?.ReloadData();
			
            // Adjust the scroll position
            var availablePositions = GetNumberOfAvailableScrollPositions();
            if (originalFirstVisibleItemIndexPath != null && originalFirstVisibleItemIndexPath.Row > availablePositions)
            {
                var newIndexPath = NSIndexPath.FromRowSection(availablePositions - positionDelta, originalFirstVisibleItemIndexPath.Section);
                ((UICollectionView)PlatformView).ScrollToItem(newIndexPath, UICollectionViewScrollPosition.Top, false);
            }
		});
		
	}

    private int GetCurrentScrollPositionDelta()
    {
        var availablePositions = GetNumberOfAvailableScrollPositions();
        var firstVisibleItemIndexPath = ((UICollectionView)PlatformView).IndexPathsForVisibleItems.OrderBy(ip => ip.Row).FirstOrDefault();
        return firstVisibleItemIndexPath != null ? availablePositions - firstVisibleItemIndexPath.Row : 0;
    }

    private int GetNumberOfAvailableScrollPositions()
    {
        return (int)(dataSource?.GetItemsCount(((UICollectionView)PlatformView), 0) ?? 0);
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
