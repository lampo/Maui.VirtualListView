using Foundation;
using UIKit;

namespace Microsoft.Maui;

public sealed class VirtualListViewController : UICollectionViewController, IUICollectionViewDragDelegate, IUICollectionViewDropDelegate
{
    private VirtualListViewHandler Handler;
    private CvLayout Layout;

    UILongPressGestureRecognizer _longPressGestureRecognizer;

    public VirtualListViewController(VirtualListViewHandler handler) : base(new CvLayout(handler))
    {
        Handler = handler;
        Layout = (CvLayout)CollectionView.CollectionViewLayout;

        Layout.ScrollDirection = handler.VirtualView.Orientation switch
        {
            ListOrientation.Vertical => UICollectionViewScrollDirection.Vertical,
            ListOrientation.Horizontal => UICollectionViewScrollDirection.Horizontal,
            _ => UICollectionViewScrollDirection.Vertical
        };
        Layout.EstimatedItemSize = UICollectionViewFlowLayout.AutomaticSize;
        Layout.ItemSize = UICollectionViewFlowLayout.AutomaticSize;
        Layout.SectionInset = new UIEdgeInsets(0, 0, 0, 0);
        Layout.MinimumInteritemSpacing = 0f;
        Layout.MinimumLineSpacing = 0f;

        DataSource = new CvDataSource(handler);
        CollectionView.DataSource = DataSource;
        CollectionView.Delegate = new CvDelegate(handler, this);

        CollectionView.DragDelegate = this;
        CollectionView.DropDelegate = this;

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
                break;
            case UIGestureRecognizerState.Changed:
                gestureRecognizer.CancelsTouchesInView = true;
                collectionView.UpdateInteractiveMovement(location);
                break;
            case UIGestureRecognizerState.Ended:
                collectionView.EndInteractiveMovement();
                DataSource.SuspendReload = false;
                Handler.IsDragging = false;
                break;
            default:
                collectionView.CancelInteractiveMovement();
                DataSource.SuspendReload = false;
                Handler.IsDragging = false;
                break;
        }
    }
}