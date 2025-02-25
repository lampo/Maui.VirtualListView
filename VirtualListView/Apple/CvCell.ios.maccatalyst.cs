using System.Diagnostics;
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

        var collectionView = this.Superview as UICollectionView;
        var layout = collectionView?.CollectionViewLayout as CvLayout;
        var originalSize = layoutAttributes.Size;
        layoutAttributes = layout?.ScrollDirection == UICollectionViewScrollDirection.Horizontal
            ? GetHorizontalLayoutAttributes(virtualView, layoutAttributes)
            : GetVerticalLayoutAttributes(virtualView, layoutAttributes);
        
        if (originalSize != layoutAttributes.Frame.Size)
        {
            layout.UpdateItemSize(layoutAttributes.IndexPath, layoutAttributes.Frame.Size);
        }

        return layoutAttributes;
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

    public override void PrepareForReuse()
    {
        PositionInfo = null;
        base.PrepareForReuse();
    }

    // TOOD: figure out how to dynamically update the layout
    // public override void SetNeedsLayout()
    // {
    //     var collectionView = this.Superview as UICollectionView;
    //     var layout = collectionView?.CollectionViewLayout as CvLayout;
    //     
    //     if (this.VirtualView is null || !this.VirtualView.TryGetTarget(out var virtualView))
    //     {
    //         base.SetNeedsLayout();
    //         return;
    //     }
    //
    //     var oldFrame = this.Frame;
    //     
    //     // check new size
    //     var newFrame = GetVerticalLayoutFrame(virtualView, this.Frame);
    //     
    //     // if the content size has changed, we need to invalidate the layout
    //     if (newFrame != oldFrame)
    //     {
    //         // layout?.InvalidateLayout();
    //     }
    // }

    public void UpdatePosition(PositionInfo positionInfo)
    {
        Debug.WriteLine("UpdatePosition: " + positionInfo.Position);
        PositionInfo = positionInfo;
        if (VirtualView?.TryGetTarget(out var virtualView) ?? false)
        {
            if (virtualView is IPositionInfo viewPositionInfo)
                viewPositionInfo.Update(positionInfo);
        }
    }

    private static UICollectionViewLayoutAttributes GetHorizontalLayoutAttributes(IView virtualView, UICollectionViewLayoutAttributes layoutAttributes)
    {
        double width;
        // slight optimization to avoid measuring the view if it's already been measured
        if (virtualView.Height is < 0 or double.NaN)
        {
            var measure = virtualView.Measure(double.PositiveInfinity, layoutAttributes.Size.Height);
            width = measure.Width;
        }
        else
        {
            width = virtualView.Width;
        }

        var frame = layoutAttributes.Frame;
        frame.Height = new nfloat(width);
        layoutAttributes.Frame = frame;

        return layoutAttributes;
    }

    private static UICollectionViewLayoutAttributes GetVerticalLayoutAttributes(IView virtualView, UICollectionViewLayoutAttributes layoutAttributes)
    {
        double height;
        // slight optimization to avoid measuring the view if it's already been measured
        if (virtualView.Height is < 0 or double.NaN)
        {
            var measure = virtualView.Measure(layoutAttributes.Size.Width, double.PositiveInfinity);
            height = measure.Height;
        }
        else
        {
            height = virtualView.Height;
        }

        var frame = layoutAttributes.Frame;
        frame.Height = new nfloat(height);
        layoutAttributes.Frame = frame;

        return layoutAttributes;
    }
    
    private static CGRect GetVerticalLayoutFrame(IView virtualView, CGRect frame)
    {
        double height;
        // slight optimization to avoid measuring the view if it's already been measured
        if (virtualView.Height is < 0 or double.NaN)
        {
            var measure = virtualView.Measure(frame.Size.Width, double.PositiveInfinity);
            height = measure.Height;
        }
        else
        {
            height = virtualView.Height;
        }

        frame.Height = new nfloat(height);

        return frame;
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
                subview.Frame = this.Bounds;
            }
        }

        public override void SetNeedsLayout()
        {
            base.SetNeedsLayout();
            parentCell.SetNeedsLayout();
        }
    }
}