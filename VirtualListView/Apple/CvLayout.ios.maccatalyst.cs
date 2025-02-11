using CoreGraphics;
using Foundation;
using UIKit;

namespace Microsoft.Maui;

internal sealed class CvLayout : UICollectionViewLayout
{
    public CvDataSource DataSource { get; set; }

    public CvLayout(VirtualListViewHandler handler)
    {
        Handler = handler;
        isiOS11 = UIDevice.CurrentDevice.CheckSystemVersion(11, 0);
    }

    readonly VirtualListViewHandler Handler;

    readonly bool isiOS11;

    private Dictionary<NSIndexPath, UICollectionViewLayoutAttributes> _cachedSizes = new();

    // public override UICollectionViewLayoutAttributes LayoutAttributesForItem(NSIndexPath path)
    // {
    //     Console.WriteLine("LayoutAttributesForItem " + path);
    //     // return base.LayoutAttributesForItem(path);
    //
    //     var key = GetIndexPathKey(path);
    //     var layoutAttributes = base.LayoutAttributesForItem(path);
    //     if (this.Handler.IsDragging && this._cachedSizes.TryGetValue(key, out var value))
    //     {
    //         Console.WriteLine("LayoutAttributesForItem cache hit " + layoutAttributes.Frame + " -> " + value.Frame);
    //         
    //         return value;
    //     }
    //     Console.WriteLine("LayoutAttributesForItem " + layoutAttributes.Frame);
    //     layoutAttributes.Frame = this.GetLayoutAttributesFrame(layoutAttributes);
    //     return layoutAttributes;
    // }

    // private (int section, int row) GetIndexPathKey(NSIndexPath path) => (path.Section, path.Row);
    private NSIndexPath GetIndexPathKey(NSIndexPath path) => path;

    // private CGRect GetLayoutAttributesFrame(UICollectionViewLayoutAttributes layoutAttributes)
    // {
    //     if (Handler.VirtualView.Orientation == ListOrientation.Vertical)
    //     {
    //         var x = SectionInset.Left;
    //
    //         NFloat width;
    //
    //         if (isiOS11)
    //             width = CollectionView.SafeAreaLayoutGuide.LayoutFrame.Width - SectionInset.Left - SectionInset.Right;
    //         else
    //             width = CollectionView.Bounds.Width - SectionInset.Left - SectionInset.Right;
    //
    //         return new CGRect(x, layoutAttributes.Frame.Y, width, layoutAttributes.Frame.Height);
    //     }
    //
    //     var y = this.SectionInset.Top;
    //
    //     NFloat height;
    //
    //     if (this.isiOS11)
    //         height = this.CollectionView.SafeAreaLayoutGuide.LayoutFrame.Height - this.SectionInset.Top - this.SectionInset.Bottom;
    //     else
    //         height = this.CollectionView.Bounds.Height - this.SectionInset.Top - this.SectionInset.Bottom;
    //
    //     return new CGRect(layoutAttributes.Frame.X, y, layoutAttributes.Frame.Width, height);
    // }

    public override void InvalidateLayout()
    {
        Console.WriteLine("InvalidateLayout");
        base.InvalidateLayout();
    }

    public override void InvalidateLayout(UICollectionViewLayoutInvalidationContext context)
    {
        Console.WriteLine("InvalidateLayout(CONTEXT) invalidate everything: " + context.InvalidateEverything);
        Console.WriteLine("InvalidateDataSourceCounts: " + context.InvalidateDataSourceCounts);
        Console.WriteLine("PreviousIndexPathsForInteractivelyMovingItems: "
                          + context.PreviousIndexPathsForInteractivelyMovingItems?.FirstOrDefault());
        Console.WriteLine("TargetIndexPathsForInteractivelyMovingItems: "
                          + context.TargetIndexPathsForInteractivelyMovingItems?.FirstOrDefault());
        Console.WriteLine("InteractiveMovementTarget: " + context.InteractiveMovementTarget);
        
        if (context is { PreviousIndexPathsForInteractivelyMovingItems: not null, TargetIndexPathsForInteractivelyMovingItems: not null })
        {
            var fromIndexPath = context.PreviousIndexPathsForInteractivelyMovingItems.FirstOrDefault();
            var toIndexPath = context.TargetIndexPathsForInteractivelyMovingItems.FirstOrDefault();
            Console.WriteLine("InvalidateLayout: from: " + fromIndexPath + " to: " + toIndexPath);
            this.SwapItemSizesWhileDragging(toIndexPath, fromIndexPath);
        }
        
        base.InvalidateLayout(context);
    }

    private List<UICollectionViewLayoutAttributes> cache = new List<UICollectionViewLayoutAttributes>();
    private nfloat contentHeight = 0;

    private nfloat ContentWidth => CollectionView?.Bounds.Width ?? 0;

    public override CGSize CollectionViewContentSize => new CGSize(ContentWidth, contentHeight);

    public override void PrepareLayout()
    {
        Console.WriteLine("PrepareLayout");
        if (CollectionView == null) return;

        int numberOfItems = (int)CollectionView.NumberOfItemsInSection(0);

        if (numberOfItems == this.cache.Count)
        {
            return;
        }

        cache.Clear();
        contentHeight = 0;

        nfloat estimatedHeight = 50; // Default estimated height

        for (int i = 0; i < numberOfItems; i++)
        {
            NSIndexPath indexPath = NSIndexPath.FromItemSection(i, 0);
            var attributes = UICollectionViewLayoutAttributes.CreateForCell(indexPath);
            attributes.Frame = new CGRect(0, contentHeight, ContentWidth, estimatedHeight);

            cache.Add(attributes);
            contentHeight += estimatedHeight;
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

    public void UpdateItemSize(NSIndexPath indexPath, nfloat newHeight)
    {
        if (indexPath.Row >= cache.Count)
        {
            return;
        }

        nfloat deltaHeight = newHeight - cache[indexPath.Row].Frame.Height;

        if (deltaHeight == 0)
        {
            return;
        }

        cache[indexPath.Row].Frame = new CGRect(0, cache[indexPath.Row].Frame.Y, ContentWidth, newHeight);

        for (int i = indexPath.Row + 1; i < cache.Count; i++)
        {
            cache[i].Frame = new CGRect(cache[i].Frame.X,
                cache[i].Frame.Y + deltaHeight,
                ContentWidth,
                cache[i].Frame.Height);
        }

        contentHeight += deltaHeight;
        InvalidateLayout();
    }

    // public override UICollectionViewLayoutAttributes GetLayoutAttributesForInteractivelyMovingItem(
    //     NSIndexPath indexPath,
    //     CGPoint targetPosition)
    // {
    //     Console.WriteLine("GetLayoutAttributesForInteractivelyMovingItem " + indexPath + " -> " + targetPosition);
    //     
    //     var attribtues = base.GetLayoutAttributesForInteractivelyMovingItem(indexPath, targetPosition);
    //     Console.WriteLine("GetLayoutAttributesForInteractivelyMovingItem " + attribtues);
    //     
    //     if (this._cachedSizes.TryGetValue(GetIndexPathKey(indexPath), out var cachedAttributes))
    //     {
    //         attribtues.Frame = new CGRect(attribtues.Frame.Location, cachedAttributes.Size);
    //     }
    //     
    //     return attribtues;
    // }
    //
    // public override UICollectionViewLayoutInvalidationContext GetInvalidationContextForInteractivelyMovingItems(
    //     NSIndexPath[] targetIndexPaths,
    //     CGPoint targetPosition,
    //     NSIndexPath[] previousIndexPaths,
    //     CGPoint previousPosition)
    // {
    //     Console.WriteLine("GetInvalidationContextForInteractivelyMovingItems");
    //     
    //     if (previousIndexPaths.Length == 0 || targetIndexPaths.Length == 0)
    //     {
    //         Console.WriteLine("GetInvalidationContextForInteractivelyMovingItems, no items");
    //         return base.GetInvalidationContextForInteractivelyMovingItems(targetIndexPaths, targetPosition, previousIndexPaths, previousPosition);
    //     }
    //
    //     var oldPath = previousIndexPaths[0];
    //     var newPath = targetIndexPaths[0];
    //     Console.WriteLine("GetInvalidationContextForInteractivelyMovingItems " + oldPath + " -> " + newPath);
    //
    //     // if (!oldPath.Equals(newPath))
    //     // {
    //     //     
    //     //     var oldAttributes = this._cachedSizes[GetIndexPathKey(oldPath)];
    //     //     var newAttributes = this._cachedSizes[GetIndexPathKey(newPath)];
    //     //     Console.WriteLine("GetInvalidationContextForInteractivelyMovingItems " + oldAttributes + " -> " + newAttributes);
    //     //     var startHeight = Math.Min(oldAttributes.Frame.Y, newAttributes.Frame.Y);
    //     //     Console.WriteLine($"Start Y: {startHeight} -> {startHeight + newAttributes.Frame.Y + oldAttributes.Frame.Y}");
    //     //     if (oldAttributes.Frame.Y < newAttributes.Frame.Y)
    //     //     {
    //     //         newAttributes.Frame = new CGRect(newAttributes.Frame.X, oldAttributes.Frame.Y, newAttributes.Frame.Width, newAttributes.Frame.Height);
    //     //         oldAttributes.Frame = new CGRect(oldAttributes.Frame.X, oldAttributes.Frame.Y + newAttributes.Frame.Height, oldAttributes.Frame.Width, oldAttributes.Frame.Height);
    //     //         Console.WriteLine("oldAttributes Less then " + oldAttributes + " -> " + newAttributes);
    //     //     }
    //     //     else
    //     //     {
    //     //         newAttributes.Frame = new CGRect(newAttributes.Frame.X, oldAttributes.Frame.Y, newAttributes.Frame.Width, newAttributes.Frame.Height);
    //     //         oldAttributes.Frame = new CGRect(oldAttributes.Frame.X, newAttributes.Frame.Y + oldAttributes.Frame.Height, oldAttributes.Frame.Width, oldAttributes.Frame.Height);
    //     //         Console.WriteLine("Old Attributes Greater then " + oldAttributes + " -> " + newAttributes);
    //     //     }
    //     //     
    //     //     startHeight = Math.Min(oldAttributes.Frame.Y, newAttributes.Frame.Y);
    //     //     Console.WriteLine($"Start Y: {startHeight} -> {startHeight + newAttributes.Frame.Y + oldAttributes.Frame.Y}");
    //     //     this._cachedSizes[GetIndexPathKey(newPath)] = oldAttributes;
    //     //     this._cachedSizes[GetIndexPathKey(oldPath)] = newAttributes;
    //     // }
    //
    //     return base.GetInvalidationContextForInteractivelyMovingItems(targetIndexPaths, targetPosition, previousIndexPaths, previousPosition);
    // }
    //
    // public override UICollectionViewLayoutAttributes[] LayoutAttributesForElementsInRect(CGRect rect)
    // {
    //     Console.WriteLine("LayoutAttributesForElementsInRect " + rect);
    //     
    //     if (this.Handler.IsDragging)
    //     {
    //         var attributes = new List<UICollectionViewLayoutAttributes>();
    //
    //         
    //         nfloat usedHeight = 0;
    //         foreach (var attribute in this._cachedSizes.Values
    //                                      .OrderBy(x => x.IndexPath.Section)
    //                                      .ThenBy(x => x.IndexPath.Row)
    //                                      .Where(attribute => attribute.Frame.IntersectsWith(rect)))
    //         {
    //             Console.WriteLine($"LayoutAttributesForElementsInRect cache hit: {attribute.IndexPath} {attribute.Frame}");
    //             attributes.Add(attribute);
    //             usedHeight += attribute.Frame.Height;
    //         }
    //
    //         if (attributes.Count != 0)
    //         {
    //             while (usedHeight < rect.Height)
    //             {
    //                 var nextIndexPath = this.GetNextIndexPath(this.CollectionView, attributes[^1].IndexPath);
    //                 if (nextIndexPath == null)
    //                 {
    //                     break;
    //                 }
    //
    //                 var nextAttribute = UICollectionViewLayoutAttributes.CreateForCell(nextIndexPath);
    //                 nextAttribute.Frame = new CGRect(0, usedHeight, rect.Width, 50);
    //                 attributes.Add(nextAttribute);
    //                 usedHeight += nextAttribute.Frame.Height;
    //             }
    //
    //             return attributes.ToArray();
    //         }
    //     }
    //     
    //     var layoutAttributesObjects = base.LayoutAttributesForElementsInRect(rect);
    //   
    //     foreach (var layoutAttributes in layoutAttributesObjects)
    //     {
    //         if (layoutAttributes.RepresentedElementCategory == UICollectionElementCategory.Cell)
    //         {
    //             Console.WriteLine($"LayoutAttributesForElementsInRect Cache miss {layoutAttributes.IndexPath}  {layoutAttributes.Frame}");
    //             layoutAttributes.Frame = this.GetLayoutAttributesFrame(layoutAttributes);
    //         }
    //     }
    //
    //     return layoutAttributesObjects;
    // }

    public NSIndexPath? GetNextIndexPath(UICollectionView collectionView, NSIndexPath currentIndexPath)
    {
        var visibleIndexPaths = collectionView.IndexPathsForVisibleItems.OrderBy(ip => ip.Row).ToList();

        if (visibleIndexPaths.Count == 0)
            return null;

        var currentIndex = visibleIndexPaths.IndexOf(currentIndexPath);

        if (currentIndex == -1 || currentIndex == visibleIndexPaths.Count - 1)
            return null;

        return visibleIndexPaths[currentIndex + 1];
    }

    // public override void PrepareLayout()
    // {
    //     Console.WriteLine("PrepareLayout");
    //     if (this.Handler.IsDragging)
    //     {
    //         // foreach (var visibleCell in this.CollectionView.VisibleCells)
    //         // {
    //         //     var indexPath = this.CollectionView.IndexPathForCell(visibleCell);
    //         //     var key = GetIndexPathKey(indexPath);
    //         //     if (this._cachedSizes.ContainsKey(key))
    //         //     {
    //         //         continue;
    //         //     }
    //         //     var newAttributes = this.LayoutAttributesForItem(indexPath);
    //         //     this._cachedSizes[key] = newAttributes;
    //         // }
    //         // nfloat y = 0;
    //         // foreach (var attribute in this._cachedSizes.Values.OrderBy(x => x.IndexPath.Section).ThenBy(x => x.IndexPath.Row))
    //         // {
    //         //     Console.WriteLine($"PrepareLayout index: {attribute.IndexPath} y: {y}, height: {attribute.Frame.Height}");
    //         //     attribute.Frame = new CGRect(attribute.Frame.X, y, attribute.Frame.Width, attribute.Frame.Height);
    //         //     y += attribute.Frame.Height;
    //         // }
    //     }
    //     base.PrepareLayout();
    // }
    //
    // public override bool ShouldInvalidateLayout(UICollectionViewLayoutAttributes preferredAttributes,
    //                                             UICollectionViewLayoutAttributes originalAttributes)
    // {
    //     var shouldInvalidate = base.ShouldInvalidateLayout(preferredAttributes, originalAttributes);
    //     Console.WriteLine("ShouldInvalidateLayout " + shouldInvalidate);
    //     Console.WriteLine($"Preferred: {preferredAttributes.IndexPath} {preferredAttributes.Frame}, Original: {originalAttributes.IndexPath} {originalAttributes.Frame}");
    //     preferredAttributes.Frame = this.GetLayoutAttributesFrame(preferredAttributes);
    //     this._cachedSizes[GetIndexPathKey(preferredAttributes.IndexPath)] = preferredAttributes;
    //     return shouldInvalidate;
    // }

    public override UICollectionViewLayoutAttributes LayoutAttributesForSupplementaryView(
        NSString kind,
        NSIndexPath indexPath)
    {
        Console.WriteLine("LayoutAttributesForSupplementaryView " + kind + " " + indexPath);
        return base.LayoutAttributesForSupplementaryView(kind, indexPath);
    }

    public override UICollectionViewLayoutAttributes FinalLayoutAttributesForDisappearingItem(NSIndexPath itemIndexPath)
    {
        var layoutAttributes = base.FinalLayoutAttributesForDisappearingItem(itemIndexPath);
        Console.WriteLine("FinalLayoutAttributesForDisappearingItem " + itemIndexPath + " -> " + layoutAttributes);
        return layoutAttributes;
    }

    public override UICollectionViewLayoutAttributes InitialLayoutAttributesForAppearingItem(NSIndexPath itemIndexPath)
    {
        var layoutAttributes = base.InitialLayoutAttributesForAppearingItem(itemIndexPath);
        Console.WriteLine("InitialLayoutAttributesForAppearingItem " + itemIndexPath + " -> " + layoutAttributes);
        return layoutAttributes;
    }

    // public void GetTargetIndexPathForMove(NSIndexPath originalIndexPath, NSIndexPath proposedIndexPath)
    // {
    //     Console.WriteLine("GetTargetIndexPathForMove " + originalIndexPath + " -> " + proposedIndexPath);
    //     var oldKey = GetIndexPathKey(originalIndexPath);
    //     var proposedKey = GetIndexPathKey(proposedIndexPath);
    //     if (!this._cachedSizes.TryGetValue(oldKey, out var oldAttributes) || !this._cachedSizes.TryGetValue(proposedKey, out var newAttributes))
    //     {
    //         return;
    //     }
    //     Console.WriteLine("GetTargetIndexPathForMove " + oldAttributes + " -> " + newAttributes);
    //     var startHeight = Math.Min(oldAttributes.Frame.Y, newAttributes.Frame.Y);
    //     Console.WriteLine($"Start Y: {startHeight} -> {startHeight + newAttributes.Frame.Y + oldAttributes.Frame.Y}");
    //     var oldY = oldAttributes.Frame.Y;
    //     var newY = newAttributes.Frame.Y;
    //     if (oldY < newY)
    //     {
    //         newAttributes.Frame = new CGRect(newAttributes.Frame.X, oldY, newAttributes.Frame.Width, newAttributes.Frame.Height);
    //         oldAttributes.Frame = new CGRect(oldAttributes.Frame.X, oldY + newAttributes.Frame.Height, oldAttributes.Frame.Width, oldAttributes.Frame.Height);
    //         Console.WriteLine("oldAttributes Less then " + oldAttributes + " -> " + newAttributes);
    //     }
    //     else
    //     {
    //         newAttributes.Frame = new CGRect(newAttributes.Frame.X, newY + oldAttributes.Frame.Height, newAttributes.Frame.Width, newAttributes.Frame.Height);
    //         oldAttributes.Frame = new CGRect(oldAttributes.Frame.X, newY, oldAttributes.Frame.Width, oldAttributes.Frame.Height);
    //         
    //         Console.WriteLine("Old Attributes Greater then " + oldAttributes + " -> " + newAttributes);
    //     }
    //         
    //     startHeight = Math.Min(oldAttributes.Frame.Y, newAttributes.Frame.Y);
    //     Console.WriteLine($"Start Y: {startHeight} -> {startHeight + newAttributes.Frame.Y + oldAttributes.Frame.Y}");
    //     
    //     Console.WriteLine($"GetTargetIndexPathForMove oldAttributes {oldAttributes.IndexPath}  {oldAttributes.Frame} newAttributes {newAttributes.IndexPath}  {newAttributes.Frame}");
    //     
    //     oldAttributes.Frame = this.GetLayoutAttributesFrame(oldAttributes);
    //     newAttributes.Frame = this.GetLayoutAttributesFrame(newAttributes);
    //     
    //     this._cachedSizes[GetIndexPathKey(proposedIndexPath)] = oldAttributes;
    //     this._cachedSizes[GetIndexPathKey(originalIndexPath)] = newAttributes;
    // }
    private NSIndexPath draggingIndexPath = null;
    private nfloat draggingItemOriginalHeight = 0;

    public void StartDraggingItem(NSIndexPath indexPath)
    {
        draggingIndexPath = indexPath;
        draggingItemOriginalHeight = cache[indexPath.Row].Frame.Height;
    }

    public void StopDraggingItem()
    {
        draggingIndexPath = null;
    }

    public void SwapItemSizesWhileDragging(NSIndexPath fromIndexPath, NSIndexPath toIndexPath)
    {
        // Console.WriteLine($"SwapItemSizesWhileDragging: {this.draggingIndexPath}, to: {toIndexPath}, cache count: {cache.Count}");
        if (fromIndexPath == null || toIndexPath == null || toIndexPath.Item >= cache.Count || fromIndexPath.Equals(toIndexPath))
        {
            return;
        }

        var fromAttributes = cache[fromIndexPath.Row];
        var toAttributes = cache[toIndexPath.Row];

        Console.WriteLine($"SwapItemSizesWhileDragging: from: {fromIndexPath} ({fromAttributes.Frame.Height}), to: {toIndexPath}  ({toAttributes.Frame.Height})");
        
        // Swap heights normally
        nfloat tempHeight = fromAttributes.Frame.Height;
        fromAttributes.Frame = new CGRect(fromAttributes.Frame.X,
            fromAttributes.Frame.Y,
            fromAttributes.Frame.Width,
            toAttributes.Frame.Height);
        
        toAttributes.Frame =
            new CGRect(toAttributes.Frame.X, toAttributes.Frame.Y, toAttributes.Frame.Width, tempHeight);

        // Swap items in cache list
        // cache[fromIndexPath.Row] = toAttributes;
        // cache[toIndexPath.Row] = fromAttributes;
        
        Console.WriteLine($"SwapItemSizesWhileDragging: to: {toIndexPath} ({fromAttributes.Frame.Height}), to: {fromIndexPath}  ({toAttributes.Frame.Height})");
        
        this.draggingIndexPath = toIndexPath;

        // resize content in cache
        nfloat calculatedY = 0;
        for (int i = 0; i < cache.Count; i++)
        {
            cache[i].Frame = new CGRect(cache[i].Frame.X, calculatedY, ContentWidth, cache[i].Frame.Height);
            // Console.WriteLine("cached Attribute: " + cache[i].Frame);
            calculatedY += cache[i].Frame.Height;
        }

        // InvalidateLayout(); // Ensure layout refresh
    }
}