using Foundation;
using UIKit;

namespace Microsoft.Maui;

public class VirtualListViewController : UICollectionViewController
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
    }

    internal CvDataSource DataSource { get; }

    public override void ItemSelected(UICollectionView collectionView, NSIndexPath indexPath)
        => HandleSelection(collectionView, indexPath, true);

    public override void ItemDeselected(UICollectionView collectionView, NSIndexPath indexPath)
        => HandleSelection(collectionView, indexPath, false);

    void HandleSelection(UICollectionView collectionView, NSIndexPath indexPath, bool selected)
    {
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
}
