using System.Diagnostics;
using CoreGraphics;
using Foundation;
using UIKit;

namespace Microsoft.Maui;

internal sealed class CvLayout : UICollectionViewLayout
{
    private nfloat dynamicContentHeight = 0;
    private nfloat dynamicContentWidth = 0;

    // Cache of layout attributes in order for fast index-based lookup.

    private List<CGRect> cache = [];

    // Previous state of the item hash codes.

    private List<int> previousItemPositionCache = [];

    // The previously observed content hash code.

    private int previousContentHashCode = 0;

    private UICollectionViewScrollDirection scrollDirection = UICollectionViewScrollDirection.Vertical;

    private readonly nfloat estimatedMeasurement = 50;

    public bool NeedsLayout { get; private set; }

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
        if (CollectionView == null)
        {
            return;
        }

        this.NeedsLayout = false;

        // We'll build a new cache list in the proper order, and a new dictionary for the updated items.
        var newCache = new List<CGRect>();
        var previousItemLookUp = new Dictionary<int, CGSize>();

        for (var i = 0; i < this.previousItemPositionCache.Count; i++)
        {
            int hashCode = this.previousItemPositionCache[i];
            CGSize size = this.cache[i].Size;
            previousItemLookUp.TryAdd(hashCode, size);
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
            
            newCache.Add(frame);

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
        var attributes = new List<UICollectionViewLayoutAttributes>();

        for (int i = 0; i < cache.Count; i++)
        {
            var frame = cache[i];
            if (frame.IntersectsWith(rect))
            {
                attributes.Add(CreateAttributes(i, frame));
            }
            
            if (frame.Y > rect.Bottom)
            {
                break;
            }
        }
        
        var tempArray = attributes.ToArray();
        
        return tempArray;
    }

    public override UICollectionViewLayoutAttributes LayoutAttributesForItem(NSIndexPath indexPath)
    {
        var attributes = UICollectionViewLayoutAttributes.CreateForCell(indexPath);
        attributes.Frame = this.cache[indexPath.Row];
        return attributes;
    }

    public override bool ShouldInvalidateLayoutForBoundsChange(CGRect newBounds) => true;

    public void UpdateItemSize(NSIndexPath indexPath, CGSize newFrame)
    {
        if (indexPath.Row >= cache.Count)
        {
            return;
        }

        var frame = cache[indexPath.Row];
        frame.Size = newFrame;
        this.cache[indexPath.Row] = frame;
        // this.RebuildCachedFramePositions();
        this.NeedsLayout = true;
        
        this.InvalidateLayout();
        this.CollectionView.SetNeedsLayout();
        this.CollectionView.SetNeedsDisplay();
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

        var fromFrame = cache[fromIndexPath.Row];
        var toFrame = cache[toIndexPath.Row];

        // Swap heights normally
        nfloat tempHeight = fromFrame.Height;
        fromFrame = new CGRect(fromFrame.X,
            fromFrame.Y,
            fromFrame.Width,
            toFrame.Height);

        toFrame =
            new CGRect(toFrame.X, toFrame.Y, toFrame.Width, tempHeight);
        
        cache[fromIndexPath.Row] = fromFrame;
        cache[toIndexPath.Row] = toFrame;

        // resize content in cache
        // this.RebuildCachedFramePositions();
    }

    private void RebuildCachedFramePositions(int startIndex = 0)
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
            nfloat calculatedY = this.cache[startIndex].Y;
            for (var index = startIndex; index < this.cache.Count; index++)
            {
                var frame = this.cache[index];
                frame.Y = calculatedY;
                this.cache[index] = frame;
                calculatedY += frame.Height;
            }

            this.dynamicContentHeight = calculatedY;
        }

        void HorizontalList()
        {
            nfloat calculatedX = 0;
            for (var index = 0; index < this.cache.Count; index++)
            {
                var frame = this.cache[index];
                frame.X = calculatedX;
                this.cache[index] = frame;
                calculatedX += frame.Width;
            }

            this.dynamicContentWidth = calculatedX;
        }
    }
    
    public CGRect GetFrame(int index)
    {
        return this.cache[index];
    }
    

    public void Dump()
    {
        Debug.WriteLine("Dumping Visible Cells");
        foreach (var cell in this.CollectionView.Subviews.OfType<CvCell>().OrderBy(cell => cell.PositionInfo?.Position ?? -1))
        {
            if (cell.PositionInfo?.Position is < 9 or > 12)
            {
                continue;
            }
            
            string label = null;
            if (cell.VirtualView.TryGetTarget(out var view) && view is BindableObject { BindingContext: not null } bindable)
            {
                var type = bindable.BindingContext.GetType();
                label = type.GetProperty("Label")?.GetValue(bindable.BindingContext)?.ToString(); 
            }

            var visibleIndex = this.CollectionView.IndexPathForCell(cell);
            Debug.WriteLine($"Position: {cell.PositionInfo?.Position ?? -1}, Index: {visibleIndex.Row}, Frame: {cell.Frame}, Label: {label}, Hidden: {cell.Hidden}");;
        }
        
        Debug.WriteLine("Dumping Layout Attributes");
        for (var index = 9; index < 13; index++)
        {
            var attr = this.cache[index];
            Debug.WriteLine($"Position: {index}, Frame: {attr}");
        }
    }
}