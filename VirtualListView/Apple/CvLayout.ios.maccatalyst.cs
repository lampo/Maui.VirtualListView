using System.Runtime.InteropServices;
using CoreGraphics;
using Foundation;
using UIKit;

namespace Microsoft.Maui;

internal sealed class CvLayout : UICollectionViewFlowLayout
{
    public CvDataSource DataSource { get; set; }
    
    public CvLayout(VirtualListViewHandler handler)
    {
        Handler = handler;
        isiOS11 = UIDevice.CurrentDevice.CheckSystemVersion(11, 0);
        this.ScrollDirection = handler.VirtualView.Orientation switch
        {
            ListOrientation.Vertical => UICollectionViewScrollDirection.Vertical,
            ListOrientation.Horizontal => UICollectionViewScrollDirection.Horizontal,
            _ => UICollectionViewScrollDirection.Vertical,
        };
        this.EstimatedItemSize = UICollectionViewFlowLayout.AutomaticSize;
        this.ItemSize = UICollectionViewFlowLayout.AutomaticSize;
        this.SectionInset = new UIEdgeInsets(0, 0, 0, 0);
        this.MinimumInteritemSpacing = 0f;
        this.MinimumLineSpacing = 0f;
    }

    readonly VirtualListViewHandler Handler;

    readonly bool isiOS11;

    private Dictionary<(int section, int row), UICollectionViewLayoutAttributes> _cachedSizes = new();
    
    public override UICollectionViewLayoutAttributes LayoutAttributesForItem(NSIndexPath path)
    {
        Console.WriteLine("LayoutAttributesForItem " + path);
        // return base.LayoutAttributesForItem(path);

        var key = GetIndexPathKey(path);
        var layoutAttributes = base.LayoutAttributesForItem(path);
        if (this.Handler.IsDragging && this._cachedSizes.TryGetValue(key, out var value))
        {
            Console.WriteLine("LayoutAttributesForItem " + layoutAttributes.Frame + " -> " + value.Frame);
            
            return layoutAttributes;
        }
        layoutAttributes.Frame = this.GetLayoutAttributesFrame(layoutAttributes);
        return layoutAttributes;
    }
    
    private (int section, int row) GetIndexPathKey(NSIndexPath path) => (path.Section, path.Row);

    private CGRect GetLayoutAttributesFrame(UICollectionViewLayoutAttributes layoutAttributes)
    {
        Console.WriteLine("UpdateLayoutAttributes " + layoutAttributes);
        if (Handler.VirtualView.Orientation == ListOrientation.Vertical)
        {
            var x = SectionInset.Left;

            NFloat width;

            if (isiOS11)
                width = CollectionView.SafeAreaLayoutGuide.LayoutFrame.Width - SectionInset.Left - SectionInset.Right;
            else
                width = CollectionView.Bounds.Width - SectionInset.Left - SectionInset.Right;

            return new CGRect(x, layoutAttributes.Frame.Y, width, layoutAttributes.Frame.Height);
        }

        var y = this.SectionInset.Top;

        NFloat height;

        if (this.isiOS11)
            height = this.CollectionView.SafeAreaLayoutGuide.LayoutFrame.Height - this.SectionInset.Top - this.SectionInset.Bottom;
        else
            height = this.CollectionView.Bounds.Height - this.SectionInset.Top - this.SectionInset.Bottom;

        return new CGRect(layoutAttributes.Frame.X, y, layoutAttributes.Frame.Width, height);
    }
    
    public override void InvalidateLayout()
    {
        Console.WriteLine("InvalidateLayout");
        base.InvalidateLayout();
        return;
        if (!Handler.IsDragging)
        {
            this._cachedSizes.Clear();
            _itemHeights.Clear(); // Only clear heights if not dragging
        }

        base.InvalidateLayout();
    }
    
    private Dictionary<(int Section, int Row), nfloat> _itemHeights = new();
  
    public override void InvalidateLayout(UICollectionViewLayoutInvalidationContext context)
    {
        Console.WriteLine("InvalidateLayout(CONTEXT) invalidate everything: " + context.InvalidateEverything);
        Console.WriteLine("InvalidateDataSourceCounts: " + context.InvalidateDataSourceCounts);
        Console.WriteLine("PreviousIndexPathsForInteractivelyMovingItems: " + context.PreviousIndexPathsForInteractivelyMovingItems);
        Console.WriteLine("TargetIndexPathsForInteractivelyMovingItems: " + context.TargetIndexPathsForInteractivelyMovingItems);
        Console.WriteLine("InteractiveMovementTarget: " + context.InteractiveMovementTarget);
        base.InvalidateLayout(context);
        return;
        if (context is RvUiCollectionViewLayoutInvalidationContext { PreviousIndexPathsForInteractivelyMovingItems: null })
        {
            return;
        }
        
        if (context is RvUiCollectionViewLayoutInvalidationContext rvContext)
        {
            var previousCell = CollectionView.CellForItem(rvContext.PreviousIndexPathsForInteractivelyMovingItems[0]);
            var targetCell = CollectionView.CellForItem(rvContext.TargetIndexPathsForInteractivelyMovingItems[0]);
            Console.WriteLine("InvalidateLayout(CONTEXT) " + previousCell + " -> " + targetCell);
            if (previousCell != null && targetCell != null)
              {
                var targetFrame = targetCell.Frame;
                var previousFrame = previousCell.Frame;
                Console.WriteLine("InvalidateLayout(CONTEXT) " + previousFrame + " -> " + targetFrame);
                targetCell.Frame = previousCell.Frame;
                previousCell.Frame = targetFrame;
                return;
            }
            return;
        }
        
        base.InvalidateLayout(context);
    }

    public override UICollectionViewLayoutAttributes GetLayoutAttributesForInteractivelyMovingItem(
        NSIndexPath indexPath,
        CGPoint targetPosition)
    {
        Console.WriteLine("GetLayoutAttributesForInteractivelyMovingItem " + indexPath + " -> " + targetPosition);
        
        var attribtues = base.GetLayoutAttributesForInteractivelyMovingItem(indexPath, targetPosition);
        Console.WriteLine("GetLayoutAttributesForInteractivelyMovingItem " + attribtues);
        
        if (this._cachedSizes.TryGetValue(GetIndexPathKey(indexPath), out var cachedAttributes))
        {
            attribtues.Frame = new CGRect(attribtues.Frame.Location, cachedAttributes.Size);
        }
        
        return attribtues;
    }

    public override UICollectionViewLayoutInvalidationContext GetInvalidationContextForInteractivelyMovingItems(
        NSIndexPath[] targetIndexPaths,
        CGPoint targetPosition,
        NSIndexPath[] previousIndexPaths,
        CGPoint previousPosition)
    {
        Console.WriteLine("GetInvalidationContextForInteractivelyMovingItems");
        
        if (previousIndexPaths.Length == 0 || targetIndexPaths.Length == 0)
        {
            Console.WriteLine("GetInvalidationContextForInteractivelyMovingItems, no items");
            return base.GetInvalidationContextForInteractivelyMovingItems(targetIndexPaths, targetPosition, previousIndexPaths, previousPosition);
        }

        var oldPath = previousIndexPaths[0];
        var newPath = targetIndexPaths[0];
        Console.WriteLine("GetInvalidationContextForInteractivelyMovingItems " + oldPath + " -> " + newPath);

        // if (!oldPath.Equals(newPath))
        // {
        //     
        //     var oldAttributes = this._cachedSizes[GetIndexPathKey(oldPath)];
        //     var newAttributes = this._cachedSizes[GetIndexPathKey(newPath)];
        //     Console.WriteLine("GetInvalidationContextForInteractivelyMovingItems " + oldAttributes + " -> " + newAttributes);
        //     var startHeight = Math.Min(oldAttributes.Frame.Y, newAttributes.Frame.Y);
        //     Console.WriteLine($"Start Y: {startHeight} -> {startHeight + newAttributes.Frame.Y + oldAttributes.Frame.Y}");
        //     if (oldAttributes.Frame.Y < newAttributes.Frame.Y)
        //     {
        //         newAttributes.Frame = new CGRect(newAttributes.Frame.X, oldAttributes.Frame.Y, newAttributes.Frame.Width, newAttributes.Frame.Height);
        //         oldAttributes.Frame = new CGRect(oldAttributes.Frame.X, oldAttributes.Frame.Y + newAttributes.Frame.Height, oldAttributes.Frame.Width, oldAttributes.Frame.Height);
        //         Console.WriteLine("oldAttributes Less then " + oldAttributes + " -> " + newAttributes);
        //     }
        //     else
        //     {
        //         newAttributes.Frame = new CGRect(newAttributes.Frame.X, oldAttributes.Frame.Y, newAttributes.Frame.Width, newAttributes.Frame.Height);
        //         oldAttributes.Frame = new CGRect(oldAttributes.Frame.X, newAttributes.Frame.Y + oldAttributes.Frame.Height, oldAttributes.Frame.Width, oldAttributes.Frame.Height);
        //         Console.WriteLine("Old Attributes Greater then " + oldAttributes + " -> " + newAttributes);
        //     }
        //     
        //     startHeight = Math.Min(oldAttributes.Frame.Y, newAttributes.Frame.Y);
        //     Console.WriteLine($"Start Y: {startHeight} -> {startHeight + newAttributes.Frame.Y + oldAttributes.Frame.Y}");
        //     this._cachedSizes[GetIndexPathKey(newPath)] = oldAttributes;
        //     this._cachedSizes[GetIndexPathKey(oldPath)] = newAttributes;
        // }

        return base.GetInvalidationContextForInteractivelyMovingItems(targetIndexPaths, targetPosition, previousIndexPaths, previousPosition);
    }

    public override UICollectionViewLayoutAttributes[] LayoutAttributesForElementsInRect(CGRect rect)
    {
        Console.WriteLine("LayoutAttributesForElementsInRect " + rect);
        
        if (this.Handler.IsDragging)
        {
            var attributes = new List<UICollectionViewLayoutAttributes>();

            
            nfloat usedHeight = 0;
            foreach (var attribute in this._cachedSizes.Values
                                         .OrderBy(x => x.IndexPath.Section)
                                         .ThenBy(x => x.IndexPath.Row)
                                         .Where(attribute => attribute.Frame.IntersectsWith(rect)))
            {
                Console.WriteLine($"LayoutAttributesForElementsInRect: {attribute.IndexPath} {attribute.Frame}");
                attributes.Add(attribute);
                usedHeight += attribute.Frame.Height;
            }

            if (attributes.Count != 0)
            {
                while (usedHeight < rect.Height)
                {
                    var nextIndexPath = this.GetNextIndexPath(this.CollectionView, attributes[^1].IndexPath);
                    if (nextIndexPath == null)
                    {
                        break;
                    }

                    var nextAttribute = UICollectionViewLayoutAttributes.CreateForCell(nextIndexPath);
                    nextAttribute.Frame = new CGRect(0, usedHeight, rect.Width, 50);
                    attributes.Add(nextAttribute);
                    usedHeight += nextAttribute.Frame.Height;
                }

                return attributes.ToArray();
            }
        }
        
        var layoutAttributesObjects = base.LayoutAttributesForElementsInRect(rect);
      
        foreach (var layoutAttributes in layoutAttributesObjects)
        {
            if (layoutAttributes.RepresentedElementCategory == UICollectionElementCategory.Cell)
            {
                layoutAttributes.Frame = this.GetLayoutAttributesFrame(layoutAttributes);
            }
        }

        return layoutAttributesObjects;
    }
    
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

    public override void PrepareLayout()
    {
        Console.WriteLine("PrepareLayout");
        if (this.Handler.IsDragging)
        {
            foreach (var visibleCell in this.CollectionView.VisibleCells)
            {
                var indexPath = this.CollectionView.IndexPathForCell(visibleCell);
                var key = GetIndexPathKey(indexPath);
                if (this._cachedSizes.ContainsKey(key))
                {
                    continue;
                }
                var newAttributes = this.LayoutAttributesForItem(indexPath);
                this._cachedSizes[key] = newAttributes;
            }
            nfloat y = 0;
            foreach (var attribute in this._cachedSizes.Values.OrderBy(x => x.IndexPath.Section).ThenBy(x => x.IndexPath.Row))
            {
                Console.WriteLine($"PrepareLayout index: {attribute.IndexPath} y: {y}, height: {attribute.Frame.Height}");
                attribute.Frame = new CGRect(attribute.Frame.X, y, attribute.Frame.Width, attribute.Frame.Height);
                y += attribute.Frame.Height;
            }
        }
        base.PrepareLayout();
    }
    
    public override bool ShouldInvalidateLayout(UICollectionViewLayoutAttributes preferredAttributes,
                                                UICollectionViewLayoutAttributes originalAttributes)
    {
        var shouldInvalidate = base.ShouldInvalidateLayout(preferredAttributes, originalAttributes);
        Console.WriteLine("ShouldInvalidateLayout " + shouldInvalidate);
        Console.WriteLine($"Preferred: {preferredAttributes.IndexPath} {preferredAttributes.Frame}, Original: {originalAttributes.IndexPath} {originalAttributes.Frame}");
        preferredAttributes.Frame = this.GetLayoutAttributesFrame(preferredAttributes);
        this._cachedSizes[GetIndexPathKey(preferredAttributes.IndexPath)] = preferredAttributes;
        return shouldInvalidate;
    }

    public override UICollectionViewLayoutAttributes LayoutAttributesForSupplementaryView(NSString kind, NSIndexPath indexPath)
    {
        Console.WriteLine("LayoutAttributesForSupplementaryView " + kind + " " + indexPath);
        return base.LayoutAttributesForSupplementaryView(kind, indexPath);
    }

    public void GetTargetIndexPathForMove(NSIndexPath originalIndexPath, NSIndexPath proposedIndexPath)
    {
        Console.WriteLine("GetTargetIndexPathForMove " + originalIndexPath + " -> " + proposedIndexPath);
        var oldKey = GetIndexPathKey(originalIndexPath);
        var proposedKey = GetIndexPathKey(proposedIndexPath);
        if (!this._cachedSizes.TryGetValue(oldKey, out var oldAttributes) || !this._cachedSizes.TryGetValue(proposedKey, out var newAttributes))
        {
            return;
        }
        Console.WriteLine("GetTargetIndexPathForMove " + oldAttributes + " -> " + newAttributes);
        var startHeight = Math.Min(oldAttributes.Frame.Y, newAttributes.Frame.Y);
        Console.WriteLine($"Start Y: {startHeight} -> {startHeight + newAttributes.Frame.Y + oldAttributes.Frame.Y}");
        var oldY = oldAttributes.Frame.Y;
        var newY = newAttributes.Frame.Y;
        if (oldY < newY)
        {
            newAttributes.Frame = new CGRect(newAttributes.Frame.X, oldY, newAttributes.Frame.Width, newAttributes.Frame.Height);
            oldAttributes.Frame = new CGRect(oldAttributes.Frame.X, oldY + newAttributes.Frame.Height, oldAttributes.Frame.Width, oldAttributes.Frame.Height);
            Console.WriteLine("oldAttributes Less then " + oldAttributes + " -> " + newAttributes);
        }
        else
        {
            newAttributes.Frame = new CGRect(newAttributes.Frame.X, newY + oldAttributes.Frame.Height, newAttributes.Frame.Width, newAttributes.Frame.Height);
            oldAttributes.Frame = new CGRect(oldAttributes.Frame.X, newY, oldAttributes.Frame.Width, oldAttributes.Frame.Height);
            
            Console.WriteLine("Old Attributes Greater then " + oldAttributes + " -> " + newAttributes);
        }
            
        startHeight = Math.Min(oldAttributes.Frame.Y, newAttributes.Frame.Y);
        Console.WriteLine($"Start Y: {startHeight} -> {startHeight + newAttributes.Frame.Y + oldAttributes.Frame.Y}");
        
        oldAttributes.Frame = this.GetLayoutAttributesFrame(oldAttributes);
        newAttributes.Frame = this.GetLayoutAttributesFrame(newAttributes);
        
        this._cachedSizes[GetIndexPathKey(proposedIndexPath)] = oldAttributes;
        this._cachedSizes[GetIndexPathKey(originalIndexPath)] = newAttributes;
    }
}


public class RvUiCollectionViewLayoutInvalidationContext : UICollectionViewFlowLayoutInvalidationContext
{
    public override bool InvalidateEverything => false;
    public override bool InvalidateDataSourceCounts => false;

    private NSIndexPath[] _previousIndexPathsForInteractivelyMovingItems;
    private NSIndexPath[] _targetIndexPathsForInteractivelyMovingItems;

    public override NSIndexPath[] PreviousIndexPathsForInteractivelyMovingItems
        => _previousIndexPathsForInteractivelyMovingItems;

    public override NSIndexPath[] TargetIndexPathsForInteractivelyMovingItems
        => this._targetIndexPathsForInteractivelyMovingItems;
    //
    // public override NSDictionary InvalidatedDecorationIndexPaths { get; }
    //
    // public override NSDictionary InvalidatedSupplementaryIndexPaths { get; }
    //
    // public override NSIndexPath[] InvalidatedItemIndexPaths { get; }
    public void SetPreviousIndexPaths(NSIndexPath[] nsIndexPaths)
    {
        _previousIndexPathsForInteractivelyMovingItems = nsIndexPaths;
    }

    public void SetTargetIndexPaths(NSIndexPath[] nsIndexPaths)
    {
        this._targetIndexPathsForInteractivelyMovingItems = nsIndexPaths;
    }
}