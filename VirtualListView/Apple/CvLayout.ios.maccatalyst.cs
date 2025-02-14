using CoreGraphics;
using Foundation;
using UIKit;

namespace Microsoft.Maui;

internal sealed class CvLayout : UICollectionViewLayout
{
    private int contentHashCode = 0;
    private List<int> itemPositionCache = [];
    
    private UICollectionViewScrollDirection scrollDirection = UICollectionViewScrollDirection.Vertical;

    public UICollectionViewScrollDirection ScrollDirection
    {
        get => this.scrollDirection;
        set
        {
            this.cache.Clear();
            this.scrollDirection = value;
        }
    }

    public override void InvalidateLayout(UICollectionViewLayoutInvalidationContext context)
    {
        if (context is
            {
                PreviousIndexPathsForInteractivelyMovingItems: not null,
                TargetIndexPathsForInteractivelyMovingItems: not null,
            })
        {
            var fromIndexPath = context.PreviousIndexPathsForInteractivelyMovingItems.FirstOrDefault();
            var toIndexPath = context.TargetIndexPathsForInteractivelyMovingItems.FirstOrDefault();
            this.SwapItemSizesWhileDragging(toIndexPath, fromIndexPath);
        }

        base.InvalidateLayout(context);
    }

    private List<UICollectionViewLayoutAttributes> cache = new();
    private nfloat dynamicContentHeight = 0;
    private nfloat dynamicContentWidth = 0;

    private nfloat ContentWidth => CollectionView?.Bounds.Width ?? 0;
    private nfloat ContentHeight => CollectionView?.Bounds.Height ?? 0;
    
    private CvDataSource DataSource => (CvDataSource)CollectionView.DataSource;

    public override CGSize CollectionViewContentSize => this.ScrollDirection == UICollectionViewScrollDirection.Vertical
        ? new CGSize(this.ContentWidth, this.dynamicContentHeight)
        : new CGSize(this.dynamicContentWidth, this.ContentHeight);

    public override void PrepareLayout()
    {
        if (CollectionView == null || DataSource.ContentHashCode == this.contentHashCode)
        {
            return;
        }

        
        int numberOfItems = (int)CollectionView.DataSource.GetItemsCount(CollectionView, 0);

        if (numberOfItems == this.cache.Count)
        {
            return;
        }

        cache.Clear();
        nfloat dynamicContentMeasurement = 0;

        nfloat estimatedMeasurement = 50; // Default estimated height

        for (int i = 0; i < numberOfItems; i++)
        {
            NSIndexPath indexPath = NSIndexPath.FromItemSection(i, 0);
            var attributes = UICollectionViewLayoutAttributes.CreateForCell(indexPath);
            attributes.Frame = this.ScrollDirection == UICollectionViewScrollDirection.Vertical
                ? new CGRect(0, dynamicContentMeasurement, ContentWidth, estimatedMeasurement)
                : new CGRect(dynamicContentMeasurement, 0, estimatedMeasurement, ContentHeight);

            cache.Add(attributes);
            dynamicContentMeasurement += estimatedMeasurement;
        }

        if (this.ScrollDirection == UICollectionViewScrollDirection.Vertical)
        {
            this.dynamicContentHeight = dynamicContentMeasurement;
        }
        else
        {
            this.dynamicContentWidth = dynamicContentMeasurement;
        }
    }

    public override UICollectionViewLayoutAttributes[] LayoutAttributesForElementsInRect(CGRect rect)
    {
        return cache.FindAll(attr => attr.Frame.IntersectsWith(rect)).ToArray();
    }

    public override UICollectionViewLayoutAttributes LayoutAttributesForItem(NSIndexPath indexPath)
    {
        return cache[indexPath.Row];
    }

    public override bool ShouldInvalidateLayoutForBoundsChange(CGRect newBounds) => true;

    public void UpdateItemSize(NSIndexPath indexPath, CGSize newFrame)
    {
        if (indexPath.Row >= cache.Count)
        {
            return;
        }

        nfloat delta = this.ScrollDirection == UICollectionViewScrollDirection.Vertical 
                ? newFrame.Height - cache[indexPath.Row].Frame.Height
            : newFrame.Width - cache[indexPath.Row].Frame.Width;

        if (delta == 0)
        {
            return;
        }

        cache[indexPath.Row].Frame = this.ScrollDirection == UICollectionViewScrollDirection.Vertical 
            ? new CGRect(0, cache[indexPath.Row].Frame.Y, ContentWidth, newFrame.Height)
            : new CGRect(cache[indexPath.Row].Frame.X, 0, newFrame.Width, ContentHeight);

        // since the content size has changed, we need to update the position of the remaning content
        this.RebuildCachedFramePositions();
    }

    public void SwapItemSizesWhileDragging(NSIndexPath fromIndexPath, NSIndexPath toIndexPath)
    {
        if (fromIndexPath == null
            || toIndexPath == null
            || toIndexPath.Item >= cache.Count
            || fromIndexPath.Equals(toIndexPath))
        {
            return;
        }

        var fromAttributes = cache[fromIndexPath.Row];
        var toAttributes = cache[toIndexPath.Row];

        // Swap heights normally
        nfloat tempHeight = fromAttributes.Frame.Height;
        fromAttributes.Frame = new CGRect(fromAttributes.Frame.X,
            fromAttributes.Frame.Y,
            fromAttributes.Frame.Width,
            toAttributes.Frame.Height);

        toAttributes.Frame =
            new CGRect(toAttributes.Frame.X, toAttributes.Frame.Y, toAttributes.Frame.Width, tempHeight);

        // resize content in cache
        this.RebuildCachedFramePositions();
    }

    private void RebuildCachedFramePositions()
    {
        if (this.ScrollDirection == UICollectionViewScrollDirection.Vertical)
        {
            VerticalList();
        }
        else
        {
            HorizontalList();
        }
        
        void VerticalList()
        {
            nfloat calculatedY = 0;
            for (int i = 0; i < cache.Count; i++)
            {
                cache[i].Frame = new CGRect(cache[i].Frame.X, calculatedY, ContentWidth, cache[i].Frame.Height);
                calculatedY += cache[i].Frame.Height;
            }
            
            this.dynamicContentHeight = calculatedY;
        }

        void HorizontalList()
        {
            nfloat calculatedX = 0;
            for (int i = 0; i < cache.Count; i++)
            {
                cache[i].Frame = new CGRect(calculatedX, cache[i].Frame.Y, cache[i].Frame.Width, ContentHeight);
                calculatedX += cache[i].Frame.Width;
            }
            
            this.dynamicContentWidth = calculatedX;
        }
    }
}