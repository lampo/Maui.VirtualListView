#nullable enable
using Foundation;
using Microsoft.Maui.Adapters;
using UIKit;

namespace Microsoft.Maui;

internal class CvDataSource : UICollectionViewDataSource
{
	public CvDataSource(VirtualListViewHandler handler)
		: base()
	{
		Handler = handler;
	}

	VirtualListViewHandler Handler { get; }

	readonly ReusableIdManager itemIdManager = new ReusableIdManager("Item");
	readonly ReusableIdManager globalIdManager = new ReusableIdManager("Global");
	readonly ReusableIdManager sectionHeaderIdManager = new ReusableIdManager("SectionHeader", new NSString("SectionHeader"));
	readonly ReusableIdManager sectionFooterIdManager = new ReusableIdManager("SectionFooter", new NSString("SectionFooter"));

	nint? cachedCount;

    public bool SuspendReload { get; set; }

    public override nint NumberOfSections(UICollectionView collectionView)
		=> 1;
	
	public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
	{
		var info = Handler?.PositionalViewSelector?.GetInfo(indexPath.Item.ToInt32());

		object? data = null;

		var nativeReuseId = CvCell.ReuseIdUnknown;
		
		if (info is not null)
		{
			data = Handler?.PositionalViewSelector?.Adapter?.DataFor(info.Kind, info.SectionIndex, info.ItemIndex);
			
			var reuseId = Handler?.PositionalViewSelector?.ViewSelector?.GetReuseId(info, data);

			nativeReuseId = info.Kind switch
			{
				PositionKind.Item => itemIdManager.GetReuseId(collectionView, reuseId),
				PositionKind.SectionHeader => sectionHeaderIdManager.GetReuseId(collectionView, reuseId),
				PositionKind.SectionFooter => sectionFooterIdManager.GetReuseId(collectionView, reuseId),
				PositionKind.Header => globalIdManager.GetReuseId(collectionView, reuseId),
				PositionKind.Footer => globalIdManager.GetReuseId(collectionView, reuseId),
				_ => CvCell.ReuseIdUnknown,
			};
		}

		var nativeCell = collectionView.DequeueReusableCell(nativeReuseId, indexPath);
		if (nativeCell is not CvCell cell)
			return (UICollectionViewCell)nativeCell;
		
		cell.SetTapHandlerCallback(TapCellHandler);
		cell.Handler = Handler;
		cell.IndexPath = new WeakReference<NSIndexPath>(indexPath);

		cell.ReuseCallback = new WeakReference<Action<IView>>((rv) =>
		{
			if (info is not null && (cell.VirtualView?.TryGetTarget(out var cellView) ?? false))
				Handler?.VirtualView?.ViewSelector?.ViewDetached(info, cellView);
		});

		if (info is not null)
		{
			if (info.SectionIndex < 0 || info.ItemIndex < 0)
				info.IsSelected = false;
			else
				info.IsSelected = Handler?.IsItemSelected(info.SectionIndex, info.ItemIndex) ?? false;
		
			if (cell.NeedsView)
			{
				var view = Handler?.PositionalViewSelector?.ViewSelector?.CreateView(info, data);
				if (view is not null)
					cell.SetupView(view);
			}

			cell.UpdatePosition(info);

			if (cell.VirtualView?.TryGetTarget(out var cellVirtualView) ?? false)
			{
				Handler?.PositionalViewSelector?.ViewSelector?.RecycleView(info, data, cellVirtualView);
				Handler?.VirtualView?.ViewSelector?.ViewAttached(info, cellVirtualView);
			}
		}

		return cell;
	}

	void TapCellHandler(CvCell cell)
	{
		var p = new ItemPosition(cell.PositionInfo.SectionIndex, cell.PositionInfo.ItemIndex);

		cell.PositionInfo.IsSelected = !cell.PositionInfo.IsSelected;

		if (cell.PositionInfo.IsSelected)
			Handler?.VirtualView?.SelectItem(p);
		else
			Handler?.VirtualView?.DeselectItem(p);
	}

	public void ReloadData()
	{
        cachedCount = null;
    }
	public override nint GetItemsCount(UICollectionView collectionView, nint section)
	{
		return cachedCount ??= Handler?.PositionalViewSelector?.TotalCount ?? 0;
	}

    public override bool CanMoveItem(UICollectionView collectionView, NSIndexPath indexPath)
    {
	    var info = Handler?.PositionalViewSelector?.GetInfo(indexPath.Item.ToInt32());

	    var adapter = Handler?.PositionalViewSelector?.Adapter as IReorderableVirtualListViewAdapter;
        return info.Kind == PositionKind.Item && adapter.CanReorderItem(info);
    }

    public override void MoveItem(UICollectionView collectionView, NSIndexPath sourceIndexPath, NSIndexPath destinationIndexPath)
    {
        var sourceInfo = Handler?.PositionalViewSelector?.GetInfo(sourceIndexPath.Item.ToInt32());
        var destinationInfo = Handler?.PositionalViewSelector?.GetInfo(destinationIndexPath.Item.ToInt32());

		var adapter = Handler?.PositionalViewSelector?.Adapter as IReorderableVirtualListViewAdapter;

		adapter.OnMoveItem(sourceInfo, destinationInfo);
	}
}