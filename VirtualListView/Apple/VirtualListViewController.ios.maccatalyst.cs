using CoreGraphics;
using Foundation;
using Microsoft.Maui.Adapters;
using UIKit;

namespace Microsoft.Maui;

public sealed class VirtualListViewController : UICollectionViewController,
    IUICollectionViewDragDelegate,
    IUICollectionViewDropDelegate
{
    private VirtualListViewHandler Handler;

    UILongPressGestureRecognizer _longPressGestureRecognizer;

    public Action<nfloat, nfloat> ScrollHandler { get; set; }

    public VirtualListViewController(VirtualListViewHandler handler)
        : base(new CvLayout())
    {
        Handler = handler;

        DataSource = new CvDataSource(handler);

        CollectionView.DataSource = DataSource;
        CollectionView.Delegate = this;

        CollectionView.DragDelegate = this;
        CollectionView.DropDelegate = this;
        CollectionView.DragInteractionEnabled = true;

        // The UICollectionViewController has built-in recognizer for reorder that can be installed by setting "InstallsStandardGestureForInteractiveMovement".
        // For some reason it only seemed to work when the CollectionView was inside the Flyout section of a FlyoutPage.
        // The UILongPressGestureRecognizer is simple enough to set up so let's just add our own.
        // InstallsStandardGestureForInteractiveMovement = false;
        //
        // _longPressGestureRecognizer = new UILongPressGestureRecognizer(HandleLongPress);
        // CollectionView.AddGestureRecognizer(_longPressGestureRecognizer);
    }


    internal CvDataSource DataSource { get; }
    
    // public override void WillDisplayCell(UICollectionView collectionView,
    //                                      UICollectionViewCell cell,
    //                                      NSIndexPath indexPath)
    //  {
    //     if (cell is CvCell dynamicCell && CollectionView.CollectionViewLayout is CvLayout layout)
    //     {
    //         var attributes = dynamicCell.CachedAttributes;
    //          // Console.WriteLine($"WillDisplayCell: inedexPath: {indexPath}, cachedIndex: {attributes?.IndexPath}");
    //          // layout.UpdateItemSize(indexPath, attributes.Frame.Size);
    //     }
    // }

    public override void ItemSelected(UICollectionView collectionView, NSIndexPath indexPath)
        => HandleSelection(collectionView, indexPath, true);

    public override void ItemDeselected(UICollectionView collectionView, NSIndexPath indexPath)
        => HandleSelection(collectionView, indexPath, false);

    void HandleSelection(UICollectionView collectionView, NSIndexPath indexPath, bool selected)
    {
        //UIView.AnimationsEnabled = false;
        if (collectionView.CellForItem(indexPath) is CvCell selectedCell
            && (selectedCell.PositionInfo?.Kind ?? PositionKind.Header) == PositionKind.Item)
        {
            selectedCell.UpdateSelected(selected);

            if (selectedCell.PositionInfo is not null)
            {
                var itemPos = new ItemPosition(selectedCell.PositionInfo.SectionIndex,
                    selectedCell.PositionInfo.ItemIndex);

                if (selected)
                    Handler?.VirtualView?.SelectItem(itemPos);
                else
                    Handler?.VirtualView?.DeselectItem(itemPos);
            }
        }
    }

    public override void Scrolled(UIScrollView scrollView)
    {
        ScrollHandler?.Invoke(scrollView.ContentOffset.X, scrollView.ContentOffset.Y);
    }

    public override bool ShouldSelectItem(UICollectionView collectionView, NSIndexPath indexPath)
        => IsRealItem(indexPath);

    public override bool ShouldDeselectItem(UICollectionView collectionView, NSIndexPath indexPath)
        => IsRealItem(indexPath);

    bool IsRealItem(NSIndexPath indexPath)
    {
        var info = Handler?.PositionalViewSelector?.GetInfo(indexPath.Item.ToInt32());
        return (info?.Kind ?? PositionKind.Header) == PositionKind.Item;
    }

#region Drag & Drop

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
        if (info.Kind != PositionKind.Item || !((IReorderableVirtualListViewAdapter)this.Handler.VirtualView.Adapter).CanReorderItem(info))
        {
            return [];
        }
            
        var item = new NSString($"{indexPath.Item}");
        var dragItem = new UIDragItem(new NSItemProvider(item));
        dragItem.LocalObject = indexPath;
        session.LocalContext = indexPath;
        // layout.StartDraggingItem(indexPath);
            
            
        return [dragItem];
    }

    void IUICollectionViewDragDelegate.DragSessionWillBegin(UICollectionView collectionView, IUIDragSession session)
    {
        if (session.Items.Length == 0)
        {
            return;
        }
        
        
    }
    
    public UICollectionViewDropProposal DropSessionDidUpdate(UICollectionView collectionView,
                                                             IUIDropSession session,
                                                             NSIndexPath destinationIndexPath)
    {
        // Console.WriteLine($"DropSessionDidUpdate {destinationIndexPath}");
        if (session.LocalDragSession is not {LocalContext: NSIndexPath fromLocation} || destinationIndexPath is null)
            return new UICollectionViewDropProposal(UIDropOperation.Forbidden); // Only allow internal drags
 
        var desinationInfo = Handler.PositionalViewSelector.GetInfo(destinationIndexPath.Item.ToInt32());
        var fromInfo = Handler.PositionalViewSelector.GetInfo(fromLocation.Item.ToInt32());
        if (desinationInfo.Kind != PositionKind.Item || !((IReorderableVirtualListViewAdapter
                )this.Handler.VirtualView.Adapter).OnMoveItem(fromInfo, desinationInfo))
        {
            return new UICollectionViewDropProposal(UIDropOperation.Forbidden); // Only allow dropping on items
        }

        session.LocalDragSession.LocalContext = destinationIndexPath;
        // this.Layout.SwapItemSizesWhileDragging(destinationIndexPath);
        return new UICollectionViewDropProposal(UIDropOperation.Move,
            UICollectionViewDropIntent.InsertAtDestinationIndexPath);
    }

    public void PerformDrop(UICollectionView collectionView, IUICollectionViewDropCoordinator coordinator)
    {
        Console.WriteLine("PerformDrop");
        NSIndexPath destinationIndexPath = coordinator.DestinationIndexPath ?? NSIndexPath.FromItemSection(0, 0);

        if (coordinator.Items.Length > 0 && coordinator.Items[0].DragItem.LocalObject is NSIndexPath sourceIndexPath)
        {
            var layout = collectionView.CollectionViewLayout as CvLayout;
            // layout?.StopDraggingItem(); // Stop tracking when drop finishes

            collectionView.PerformBatchUpdates(() =>
                {
                    collectionView.MoveItem(sourceIndexPath, destinationIndexPath);
                },
                null);
        }
    }
    
    void HandleLongPress(UILongPressGestureRecognizer gestureRecognizer)
    {
        Console.WriteLine("HandleLongPress: " + gestureRecognizer.State);
        var collectionView = CollectionView;
        if (collectionView == null)
            return;

        var location = gestureRecognizer.LocationInView(collectionView);

        // We are updating "CancelsTouchesInView" so views can still receive touch events when this gesture runs.
        // Those events shouldn't be aborted until they've actually moved the position of the CollectionView item.
        switch (gestureRecognizer.State)
        {
            case UIGestureRecognizerState.Began:
                var indexPath = collectionView?.IndexPathForItemAtPoint(location);
                var adaptor = this.Handler.PositionalViewSelector.Adapter as IReorderableVirtualListViewAdapter;
                var info = this.Handler.PositionalViewSelector.GetInfo(indexPath.Item.ToInt32());
                if (indexPath == null || info.Kind != PositionKind.Item || !(adaptor?.CanReorderItem(info) ?? false))
                {
                    return;
                }

                gestureRecognizer.CancelsTouchesInView = false;
                collectionView.BeginInteractiveMovementForItem(indexPath);
                break;
            case UIGestureRecognizerState.Changed:
                gestureRecognizer.CancelsTouchesInView = true;
                location = this.Handler.VirtualView.Orientation == ListOrientation.Vertical ? new CGPoint(0, location.Y) : new CGPoint(location.X, 0);
                collectionView.UpdateInteractiveMovement(location);
                break;
            case UIGestureRecognizerState.Ended:
                collectionView.EndInteractiveMovement();
                DataSource.SuspendReload = false;
                break;
            default:
                collectionView.CancelInteractiveMovement();
                DataSource.SuspendReload = false;
                break;
        }
    }

#endregion
}
