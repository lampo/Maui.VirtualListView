using CoreFoundation;
using CoreGraphics;
using Foundation;
using Microsoft.Maui.Platform;
using UIKit;

namespace Microsoft.Maui;

internal class CvCell : UICollectionViewCell
{
    internal const string ReuseIdUnknown = "UNKNOWN";

    public VirtualListViewHandler Handler { get; set; }

    public PositionInfo PositionInfo { get; private set; }

    public WeakReference<Action<IView>> ReuseCallback { get; set; }

    private WeakReference<UICollectionViewLayoutAttributes> cachedAttributes;

    [Export("initWithFrame:")]
    public CvCell(CGRect frame)
        : base(frame)
    {
        this.ContentView.AddGestureRecognizer(new UITapGestureRecognizer(() => InvokeTap()));
    }

    private TapHandlerCallback TapHandler;

    public void SetTapHandlerCallback(Action<CvCell> callback)
    {
        TapHandler = new TapHandlerCallback(callback);
    }

    WeakReference<UIKeyCommand[]> keyCommands;
    private NSIndexPath indexPath;

    public override UIKeyCommand[] KeyCommands
    {
        get
        {
            if (keyCommands?.TryGetTarget(out var commands) ?? false)
                return commands;

            var v = new[]
            {
                UIKeyCommand.Create(new NSString("\r"), 0, new ObjCRuntime.Selector("keyCommandSelect")),
                UIKeyCommand.Create(new NSString(" "), 0, new ObjCRuntime.Selector("keyCommandSelect")),
            };

            keyCommands = new WeakReference<UIKeyCommand[]>(v);

            return v;
        }
    }

    [Export("keyCommandSelect")]
    public void KeyCommandSelect()
    {
        InvokeTap();
    }

    void InvokeTap()
    {
        if (PositionInfo.Kind == PositionKind.Item)
        {
            TapHandler.Invoke(this);
        }
    }

    public void UpdateSelected(bool selected)
    {
        PositionInfo.IsSelected = selected;

        if (VirtualView?.TryGetTarget(out var virtualView) ?? false)
        {
            if (virtualView is IPositionInfo positionInfo)
            {
                positionInfo.IsSelected = selected;
                virtualView.Handler?.UpdateValue(nameof(PositionInfo.IsSelected));
            }
        }
    }

    public override UICollectionViewLayoutAttributes PreferredLayoutAttributesFittingAttributes(
        UICollectionViewLayoutAttributes layoutAttributes)
    {
        if ((this.NativeView is null || !this.NativeView.TryGetTarget(out _))
            || (this.VirtualView is null || !this.VirtualView.TryGetTarget(out var virtualView)))
        {
            return layoutAttributes;
        }

        Console.WriteLine("UpdateItemSize: " + layoutAttributes.Frame);

        var newLayoutAttributes = base.PreferredLayoutAttributesFittingAttributes(layoutAttributes);

        var collectionView = this.Superview as UICollectionView;
        var layout = collectionView?.CollectionViewLayout as CvLayout;
        var originalSize = layoutAttributes.Size;
        var newSize = layout?.ScrollDirection == UICollectionViewScrollDirection.Horizontal
            ? GetHorizontalLayoutSize(virtualView, originalSize)
            : GetVerticalLayoutSize(virtualView, originalSize);

        if (originalSize != newSize)
        {
            var frame = newLayoutAttributes.Frame;
            frame.Size = newSize;
            newLayoutAttributes.Frame = frame;
            
            var info = Handler.PositionalViewSelector.GetInfo(newLayoutAttributes.IndexPath.Item.ToInt32());
            if (info.Kind == PositionKind.Item)
            {
                var data = Handler.PositionalViewSelector.Adapter.GetItem(info.SectionIndex, info.ItemIndex);
                if (data != ((View)virtualView).BindingContext)
                {
                    Console.WriteLine("View Not Recycled");                
                }                
            }
            
            Console.WriteLine("UpdateItemSize: " + newLayoutAttributes.IndexPath + " " + newSize + " cached Index: " + this.indexPath + " " + this);
            layout.UpdateItemSize(newLayoutAttributes.IndexPath, layoutAttributes.Frame.Size);
        }

        return newLayoutAttributes;
    }

    public override void PrepareForReuse()
    {
        Console.WriteLine("PrepareForReuse: " + this.indexPath + " " + this);
        this.indexPath = null;
        base.PrepareForReuse();
    }

    public bool NeedsView
        => NativeView == null
           || VirtualView is null
           || !NativeView.TryGetTarget(out var _)
           || !VirtualView.TryGetTarget(out var _);

    public WeakReference<IView> VirtualView { get; set; }

    public WeakReference<UIView> NativeView { get; set; }

    public override void ApplyLayoutAttributes(UICollectionViewLayoutAttributes? layoutAttributes)
    {
        this.cachedAttributes = new WeakReference<UICollectionViewLayoutAttributes>(layoutAttributes);
        base.ApplyLayoutAttributes(layoutAttributes);
    }

    public void SetupView(IView view)
    {
        // Create a new platform native view if we don't have one yet
        if (!(NativeView?.TryGetTarget(out var _) ?? false))
        {
            var container = new ViewContainer(this);
            var nativeView = view.ToPlatform(this.Handler.MauiContext);

            container.Frame = this.ContentView.Frame;
            container.AutoresizingMask = UIViewAutoresizing.FlexibleHeight | UIViewAutoresizing.FlexibleWidth;
            nativeView.AutoresizingMask = UIViewAutoresizing.FlexibleHeight | UIViewAutoresizing.FlexibleWidth;
            nativeView.ContentMode = UIViewContentMode.Redraw;

            container.AddSubview(nativeView);
            this.AddSubview(container);

            NativeView = new WeakReference<UIView>(nativeView);
        }

        if (!(VirtualView?.TryGetTarget(out var virtualView) ?? false) || (virtualView?.Handler is null))
        {
            VirtualView = new WeakReference<IView>(view);
        }

        this.SetNeedsLayout();
    }

    public override void SetNeedsLayout()
    {
        var collectionView = this.Superview as UICollectionView;
        var layout = collectionView?.CollectionViewLayout as CvLayout;

        if (this.Hidden || this.Bounds.IsEmpty)
        {
            //base.SetNeedsLayout();
            return;
        }

        if (this.indexPath is null
            || this.VirtualView is null
            || !this.VirtualView.TryGetTarget(out var virtualView))
        {
            //layout?.InvalidateLayout();
            base.SetNeedsLayout();
            return;
        }

        var originalSize = this.Bounds.Size;

        // check new size
        var preferredSize = layout?.ScrollDirection == UICollectionViewScrollDirection.Horizontal
            ? GetHorizontalLayoutSize(virtualView, originalSize)
            : GetVerticalLayoutSize(virtualView, originalSize);

        // if the content size has changed, we need to invalidate the layout
        if (preferredSize != originalSize)
        {
            Console.WriteLine("SetNeedsLayout: " + this.indexPath + " " + originalSize + " " + preferredSize + " " + this);
           // layout.LayoutIfNeeded(this.indexPath, preferredSize);
        }
    }

    public void UpdatePosition(PositionInfo positionInfo, NSIndexPath indexPath)
    {
        Console.WriteLine($"UpdatePosition: old: {this.indexPath}, new: {indexPath}");
        this.indexPath = indexPath;
        PositionInfo = positionInfo;
        if (VirtualView?.TryGetTarget(out var virtualView) ?? false)
        {
            if (virtualView is IPositionInfo viewPositionInfo)
                viewPositionInfo.Update(positionInfo);
        }
    }

    private static CGSize GetHorizontalLayoutSize(IView virtualView, CGSize size)
    {
        double width;

        // slight optimization to avoid measuring the view if it's already been measured
        if (virtualView.Height is < 0 or double.NaN)
        {
            var measure = virtualView.Measure(double.PositiveInfinity, size.Height);
            width = measure.Width;
        }
        else
        {
            width = virtualView.Width;
        }

        size.Width = new nfloat(width);

        return size;
    }

    private static CGSize GetVerticalLayoutSize(IView virtualView, CGSize originalSize)
    {
        double height;

        // slight optimization to avoid measuring the view if it's already been measured
        if (virtualView.Height is < 0 or double.NaN)
        {
            var measure = virtualView.Measure(originalSize.Width, double.PositiveInfinity);
            height = measure.Height;
        }
        else
        {
            height = virtualView.Height;
        }

        originalSize.Height = new nfloat(height);

        return originalSize;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            if (NativeView?.TryGetTarget(out var nativeView) ?? false)
            {
                nativeView.RemoveFromSuperview();
                nativeView.Dispose();
            }

            if (VirtualView?.TryGetTarget(out var virtualView) ?? false)
            {
                virtualView.Handler = null;
            }

            NativeView = null;
            VirtualView = null;
            TapHandler = null;
        }
    }

    class TapHandlerCallback
    {
        public TapHandlerCallback(Action<CvCell> callback)
        {
            Callback = callback;
        }

        public readonly Action<CvCell> Callback;

        public void Invoke(CvCell cell)
            => Callback?.Invoke(cell);
    }

    private class ViewContainer(CvCell parentCell) : UIView
    {
        public override void LayoutSubviews()
        {
            base.LayoutSubviews();

            // Adjust constraints of subviews if needed
            foreach (var subview in Subviews)
            {
                subview.Frame = Bounds;
            }
        }

        public override void SetNeedsLayout()
        {
            base.SetNeedsLayout();
            parentCell.SetNeedsLayout();
        }
    }
}