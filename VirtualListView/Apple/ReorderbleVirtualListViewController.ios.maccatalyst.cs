using Foundation;
using Microsoft.Maui.Adapters;
using UIKit;

namespace Microsoft.Maui;

public class ReorderbleVirtualListViewController : VirtualListViewController,
    IUICollectionViewDragDelegate,
    IUICollectionViewDropDelegate
{
    private readonly ReordableVirtualListViewHandler Handler;

    public ReorderbleVirtualListViewController(ReordableVirtualListViewHandler handler)
        : base(handler)
    {
        this.Handler = handler;
        this.CollectionView.DragDelegate = this;
        this.CollectionView.DropDelegate = this;
    }

    public bool IsDragging { get; private set; }

    private IReorderbleVirtualListView VirtualView => (IReorderbleVirtualListView)this.Handler.VirtualView;

    UIDragPreviewParameters? IUICollectionViewDragDelegate.GetDragPreviewParameters(
        UICollectionView collectionView,
        NSIndexPath indexPath)
    {
        var parameters = new UIDragPreviewParameters();
        var cell = collectionView.CellForItem(indexPath);
        if (cell is CvCell cvCell)
        {
            parameters.VisiblePath = UIBezierPath.FromRoundedRect(cvCell.Bounds, 8);
        }

        return parameters;
    }

    UIDragItem[] IUICollectionViewDragDelegate.GetItemsForBeginningDragSession(UICollectionView collectionView,
                                                                               IUIDragSession session,
                                                                               NSIndexPath indexPath)
    {
        var info = this.Handler.PositionalViewSelector.GetInfo(indexPath.Item.ToInt32());
        if (info.Kind != PositionKind.Item
            || !((IReorderableVirtualListViewAdapter)this.Handler.VirtualView.Adapter).CanReorderItem(info))
        {
            return [];
        }

        this.IsDragging = true;

        var item = new NSString($"{indexPath.Item}");
        var dragItem = new UIDragItem(new NSItemProvider(item));
        dragItem.LocalObject = indexPath;
        session.LocalContext = indexPath;
        return [dragItem];
    }

    void IUICollectionViewDragDelegate.DragSessionDidEnd(UICollectionView collectionView, IUIDragSession session)
        => this.IsDragging = false;

    UICollectionViewDropProposal IUICollectionViewDropDelegate.DropSessionDidUpdate(UICollectionView collectionView,
        IUIDropSession session,
        NSIndexPath destinationIndexPath)
    {
        if (session.LocalDragSession is not { LocalContext: NSIndexPath fromLocation } || destinationIndexPath is null)
            return new UICollectionViewDropProposal(UIDropOperation.Forbidden); // Only allow internal drags

        var desinationInfo = this.Handler.PositionalViewSelector.GetInfo(destinationIndexPath.Item.ToInt32());
        var fromInfo = this.Handler.PositionalViewSelector.GetInfo(fromLocation.Item.ToInt32());
        if (desinationInfo.Kind != PositionKind.Item
            || !((IReorderableVirtualListViewAdapter
                )this.Handler.VirtualView.Adapter).OnMoveItem(fromInfo, desinationInfo))
        {
            return new UICollectionViewDropProposal(UIDropOperation.Forbidden); // Only allow dropping on items
        }

        session.LocalDragSession.LocalContext = destinationIndexPath;
        return new UICollectionViewDropProposal(UIDropOperation.Move,
            UICollectionViewDropIntent.InsertAtDestinationIndexPath);
    }

    void IUICollectionViewDropDelegate.PerformDrop(UICollectionView collectionView,
                                                   IUICollectionViewDropCoordinator coordinator)
    {
        NSIndexPath destinationIndexPath = coordinator.DestinationIndexPath ?? NSIndexPath.FromItemSection(0, 0);

        if (coordinator.Items.Length > 0 && coordinator.Items[0].DragItem.LocalObject is NSIndexPath sourceIndexPath)
        {
            var desinationInfo = this.Handler.PositionalViewSelector.GetInfo(destinationIndexPath.Item.ToInt32());
            var fromInfo = this.Handler.PositionalViewSelector.GetInfo(sourceIndexPath.Item.ToInt32());
            this.VirtualView.Adapter.OnReorderComplete(fromInfo.SectionIndex,
                fromInfo.ItemIndex,
                desinationInfo.SectionIndex,
                desinationInfo.ItemIndex);
        }
    }
}