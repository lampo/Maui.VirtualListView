using CoreGraphics;
using Foundation;
using UIKit;

namespace Microsoft.Maui;

public sealed class VirtualListViewController : UICollectionViewController, IUICollectionViewDragDelegate, IUICollectionViewDropDelegate, IUICollectionViewDelegateFlowLayout
{
    private VirtualListViewHandler Handler;
    private CvLayout Layout;

    UILongPressGestureRecognizer _longPressGestureRecognizer;
    
    public Action<nfloat, nfloat> ScrollHandler { get; set; }

    public VirtualListViewController(VirtualListViewHandler handler) : base(new CvLayout(handler))
    {
        Handler = handler;
        Layout = (CvLayout)CollectionView.CollectionViewLayout;

        DataSource = new CvDataSource(handler);
        
        this.Layout.DataSource = DataSource;
        
        CollectionView.DataSource = DataSource;
        CollectionView.Delegate = this;

        CollectionView.DragDelegate = this;
        CollectionView.DropDelegate = this;
        CollectionView.DragInteractionEnabled = false;

        // The UICollectionViewController has built-in recognizer for reorder that can be installed by setting "InstallsStandardGestureForInteractiveMovement".
        // For some reason it only seemed to work when the CollectionView was inside the Flyout section of a FlyoutPage.
        // The UILongPressGestureRecognizer is simple enough to set up so let's just add our own.
        InstallsStandardGestureForInteractiveMovement = false;

        _longPressGestureRecognizer = new UILongPressGestureRecognizer(HandleLongPress);
        CollectionView.AddGestureRecognizer(_longPressGestureRecognizer);
    }

    internal CvDataSource DataSource { get; }
    
    public override UICollectionView CollectionView
    {
        get
        {
            Console.WriteLine("CollectionView get");
            return base.CollectionView;
        }
        set
        {
            Console.WriteLine("CollectionView set");
            base.CollectionView = value;
        }
    }

    public override bool CanMoveItem(UICollectionView collectionView, NSIndexPath indexPath)
    {
        Console.WriteLine($"CanMoveItem {indexPath}");
        DataSource.SuspendReload = true;
        Handler.IsDragging = true;
        return true;
    }

    public override void MoveItem(UICollectionView collectionView, NSIndexPath sourceIndexPath, NSIndexPath destinationIndexPath)
    {
        Console.WriteLine($"MoveItem {sourceIndexPath} -> {destinationIndexPath}");
        DataSource.MoveItem(collectionView, sourceIndexPath, destinationIndexPath);
        //base.MoveItem(collectionView, sourceIndexPath, destinationIndexPath);
    }

    public override NSIndexPath GetTargetIndexPathForMove(UICollectionView collectionView,
                                                          NSIndexPath originalIndexPath,
                                                          NSIndexPath proposedIndexPath)
    {
        this.Layout.GetTargetIndexPathForMove(originalIndexPath, proposedIndexPath);
        
        return proposedIndexPath;
    }

    // Allow dropping and reordering
    public void PerformDrop(UICollectionView collectionView, IUICollectionViewDropCoordinator coordinator)
    {
        Console.WriteLine("PerformDrop");
        var destinationIndexPath = coordinator.DestinationIndexPath ?? NSIndexPath.FromItemSection(0, 0);

        foreach (var item in coordinator.Items)
        {
            if (item.SourceIndexPath != null)
            {
                var sourceIndexPath = item.SourceIndexPath;
                DataSource.MoveItem(collectionView, sourceIndexPath, destinationIndexPath);
                CollectionView.PerformBatchUpdates(() =>
                {
                    CollectionView.MoveItem(sourceIndexPath, destinationIndexPath);
                }, null);
            }
        }
    }
    
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
                var itemPos = new ItemPosition(
                    selectedCell.PositionInfo.SectionIndex,
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
                if (indexPath == null)
                {
                    return;
                }
                gestureRecognizer.CancelsTouchesInView = false;
                collectionView.BeginInteractiveMovementForItem(indexPath);
                this.Handler.IsDragging = true;
                break;
            case UIGestureRecognizerState.Changed:
                gestureRecognizer.CancelsTouchesInView = true;
                collectionView.UpdateInteractiveMovement(location);
                break;
            case UIGestureRecognizerState.Ended:
                collectionView.EndInteractiveMovement();
                DataSource.SuspendReload = false;
                Handler.IsDragging = false;
                //this.Layout.InvalidateLayout();
                break;
            default:
                collectionView.CancelInteractiveMovement();
                DataSource.SuspendReload = false;
                Handler.IsDragging = false;
                //this.Layout.InvalidateLayout();
                break;
        }
    }
}