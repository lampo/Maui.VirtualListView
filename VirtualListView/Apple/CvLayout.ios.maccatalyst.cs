using System.Runtime.InteropServices;
using CoreGraphics;
using Foundation;
using UIKit;

namespace Microsoft.Maui;

internal class CvLayout : UICollectionViewFlowLayout
{
    public CvDataSource DataSource { get; set; }
    
    public CvLayout(VirtualListViewHandler handler)
    {
        Handler = handler;
        isiOS11 = UIDevice.CurrentDevice.CheckSystemVersion(11, 0);
    }

    readonly VirtualListViewHandler Handler;

    readonly bool isiOS11;

    private Dictionary<NSIndexPath, UICollectionViewLayoutAttributes> _cachedAttributes = new Dictionary<NSIndexPath, UICollectionViewLayoutAttributes>();
    
    public override UICollectionViewLayoutAttributes LayoutAttributesForItem(NSIndexPath path)
    {
        var layoutAttributes = base.LayoutAttributesForItem(path);

        if (Handler.VirtualView.Orientation == ListOrientation.Vertical)
        {
            var x = SectionInset.Left;

            NFloat width;

            if (isiOS11)
                width = CollectionView.SafeAreaLayoutGuide.LayoutFrame.Width - SectionInset.Left - SectionInset.Right;
            else
                width = CollectionView.Bounds.Width - SectionInset.Left - SectionInset.Right;

            layoutAttributes.Frame = new CGRect(x, layoutAttributes.Frame.Y, width, layoutAttributes.Frame.Height);
        }
        else
        {
            var y = SectionInset.Top;

            NFloat height;

            if (isiOS11)
                height = CollectionView.SafeAreaLayoutGuide.LayoutFrame.Height - SectionInset.Top - SectionInset.Bottom;
            else
                height = CollectionView.Bounds.Height - SectionInset.Top - SectionInset.Bottom;

            layoutAttributes.Frame = new CGRect(layoutAttributes.Frame.X, y, layoutAttributes.Frame.Width, height);
        }
        var cell = CollectionView.CellForItem(path);
        
        Console.WriteLine($"LayoutAttributesForItem {path}, cell: {cell}, layoutAttributes: {layoutAttributes}");
        if (cell != null)
        {
            // height = this.CalculateCellHeight(cell, width);
            // _itemHeights[key] = height;
            Console.WriteLine("Cell Present");
        }
        // else
        // {
        //     height = layoutAttributes.Frame.Height;
        // }
        return layoutAttributes;
        // Console.WriteLine("LayoutAttributesForItem " + path);
        //
        // if (_cachedAttributes.TryGetValue(path, out var cachedAttributes))
        // {
        //     Console.WriteLine("Cache HIT for " + path);
        //     return cachedAttributes;
        // }
        //
        // Console.WriteLine("Cache MISS for " + path);
        // var layoutAttributes = (UICollectionViewLayoutAttributes)base.LayoutAttributesForItem(path).Copy(); // Avoid mutating base attributes
        //
        // nfloat width;
        // if (isiOS11)
        //     width = CollectionView.SafeAreaLayoutGuide.LayoutFrame.Width - SectionInset.Left - SectionInset.Right;
        // else
        //     width = CollectionView.Bounds.Width - SectionInset.Left - SectionInset.Right;
        //
        // var key = (path.Section, path.Row);
        // if (!_itemHeights.TryGetValue(key, out var height))
        // {
        //     var cell = CollectionView.CellForItem(path);
        //
        //     if (cell != null)
        //     {
        //         height = this.CalculateCellHeight(cell, width);
        //         _itemHeights[key] = height;
        //     }
        //     else
        //     {
        //         height = layoutAttributes.Frame.Height;
        //     }
        // }
        //
        // layoutAttributes.Frame = new CGRect(layoutAttributes.Frame.X, layoutAttributes.Frame.Y, width, height);
        // _cachedAttributes[path] = layoutAttributes; // Cache the attributes
        //
        // return layoutAttributes;
    }


    
    private nfloat CalculateCellHeight(UICollectionViewCell cell, nfloat width)
    {
        // Force layout to get accurate size
        cell.SetNeedsLayout();
        cell.LayoutIfNeeded();

        // Assume the cell’s contentView height is the height we need
        return cell.ContentView.Frame.Height;
    }

    
    public override void InvalidateLayout()
    {
        Console.WriteLine("InvalidateLayout");
        if (!Handler.IsDragging)
        {
            _cachedAttributes.Clear();
            _itemHeights.Clear(); // Only clear heights if not dragging
        }

        base.InvalidateLayout();
    }
    
    private Dictionary<(int Section, int Row), nfloat> _itemHeights = new();

    public override void InvalidateLayout(UICollectionViewLayoutInvalidationContext context)
    {
        Console.WriteLine("InvalidateLayout(CONTEXT) invalidate everything: " + context.InvalidateEverything);
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

    public override UICollectionViewLayoutInvalidationContext GetInvalidationContextForInteractivelyMovingItems(
        NSIndexPath[] targetIndexPaths,
        CGPoint targetPosition,
        NSIndexPath[] previousIndexPaths,
        CGPoint previousPosition)
    {
        if (previousIndexPaths.Length == 0 || targetIndexPaths.Length == 0)
        {
            Console.WriteLine("GetInvalidationContextForInteractivelyMovingItems, no items");
            return new RvUiCollectionViewLayoutInvalidationContext();
        }

        var oldPath = previousIndexPaths[0];
        var newPath = targetIndexPaths[0];
        Console.WriteLine("GetInvalidationContextForInteractivelyMovingItems " + oldPath + " -> " + newPath);

        if (!oldPath.Equals(newPath))
        {
            var invalidationContext = new RvUiCollectionViewLayoutInvalidationContext();
            invalidationContext.SetPreviousIndexPaths(new[] { oldPath });
            invalidationContext.SetTargetIndexPaths(new[] { newPath });
            return invalidationContext;
        }

        return new RvUiCollectionViewLayoutInvalidationContext();
    }

    public override UICollectionViewLayoutAttributes[] LayoutAttributesForElementsInRect(CGRect rect)
    {
        Console.WriteLine("LayoutAttributesForElementsInRect");
        var layoutAttributesObjects = base.LayoutAttributesForElementsInRect(rect);

        foreach (var layoutAttributes in layoutAttributesObjects)
        {
            if (layoutAttributes.RepresentedElementCategory == UICollectionElementCategory.Cell)
            {
                var newAttributes = LayoutAttributesForItem(layoutAttributes.IndexPath);
                layoutAttributes.Frame = newAttributes.Frame;
            }
        }

        return layoutAttributesObjects;
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