using CoreGraphics;
using Foundation;
using UIKit;

namespace Microsoft.Maui;

internal sealed class CvLayout : UICollectionViewLayout
{
    private nfloat dynamicContentHeight = 0;
    private nfloat dynamicContentWidth = 0;

    // Cache of layout attributes in order for fast index-based lookup.
    private List<UICollectionViewLayoutAttributes> cache = [];

    // Previous state of the item hash codes.
    private List<int> previousItemPositionCache = [];

    // The previously observed content hash code.
    private int previousContentHashCode = 0;

    private UICollectionViewScrollDirection scrollDirection = UICollectionViewScrollDirection.Vertical;
    private readonly nfloat estimatedMeasurement = 50;

    public UICollectionViewScrollDirection ScrollDirection
    {
        get => this.scrollDirection;
        set
        {
            this.ClearCache();
            this.scrollDirection = value;
        }
    }

    private void ClearCache()
    {
        this.cache.Clear();
        this.previousItemPositionCache.Clear();
        this.previousContentHashCode = 0;
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

    private nfloat ContentWidth => CollectionView?.Bounds.Width ?? 0;

    private nfloat ContentHeight => CollectionView?.Bounds.Height ?? 0;

    private CvDataSource DataSource => (CvDataSource)CollectionView.DataSource;

    public override CGSize CollectionViewContentSize => this.ScrollDirection == UICollectionViewScrollDirection.Vertical
        ? new CGSize(this.ContentWidth, this.dynamicContentHeight)
        : new CGSize(this.dynamicContentWidth, this.ContentHeight);

    public override void PrepareLayout()
    {
        if (CollectionView == null || DataSource.ContentHashCode == this.previousContentHashCode)
        {
            return;
        }

        // We'll build a new cache list in the proper order, and a new dictionary for the updated items.
        var newCache = new List<UICollectionViewLayoutAttributes>();
        var previousItemLookUp = new Dictionary<int, CGSize>();

        for (var i = 0; i < this.previousItemPositionCache.Count; i++)
        {
            int hashCode = this.previousItemPositionCache[i];
            CGSize size = this.cache[i].Frame.Size;
            previousItemLookUp.Add(hashCode, size);
        }

        IReadOnlyList<int> newItemHashes = DataSource.ItemPositionCache;
        nfloat dynamicContentMeasurement = 0;

        for (var index = 0; index < newItemHashes.Count; index++)
        {
            int newHash = newItemHashes[index];

            // If the attribute exists in the cached dictionary, reuse it.
            var frame = previousItemLookUp.TryGetValue(newHash, out var size)
                ? this.CreateFrame(dynamicContentMeasurement, size)
                : this.CreateDefaultFrame(dynamicContentMeasurement);

            var attribute = CreateAttributes(index, frame);
            newCache.Add(attribute);

            dynamicContentMeasurement += this.ScrollDirection == UICollectionViewScrollDirection.Vertical
                ? frame.Height
                : frame.Width;
        }

        // Update our cache and dictionary to reflect the current state.
        cache = newCache;
        previousItemPositionCache = newItemHashes.ToList();
        previousContentHashCode = DataSource.ContentHashCode;

        if (this.ScrollDirection == UICollectionViewScrollDirection.Vertical)
        {
            this.dynamicContentHeight = dynamicContentMeasurement;
        }
        else
        {
            this.dynamicContentWidth = dynamicContentMeasurement;
        }
    }

    private static UICollectionViewLayoutAttributes CreateAttributes(int index, CGRect frame)
    {
        NSIndexPath indexPath = NSIndexPath.FromItemSection(index, 0);
        var attributes = UICollectionViewLayoutAttributes.CreateForCell(indexPath);
        attributes.Frame = frame;
        return attributes;
    }

    private CGRect CreateFrame(nfloat dynamicContentMeasurement, CGSize size)
    {
        return this.ScrollDirection == UICollectionViewScrollDirection.Vertical
            ? new CGRect(0, dynamicContentMeasurement, ContentWidth, size.Height)
            : new CGRect(dynamicContentMeasurement, 0, size.Width, ContentHeight);
    }

    private CGRect CreateDefaultFrame(nfloat dynamicContentMeasurement)
    {
        return this.ScrollDirection == UICollectionViewScrollDirection.Vertical
            ? new CGRect(0, dynamicContentMeasurement, ContentWidth, estimatedMeasurement)
            : new CGRect(dynamicContentMeasurement, 0, estimatedMeasurement, ContentHeight);
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