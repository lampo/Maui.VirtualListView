﻿using CoreGraphics;
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
        layoutAttributes.Frame = layout?.ScrollDirection == UICollectionViewScrollDirection.Horizontal
            ? GetHorizontalLayoutFrame(virtualView, layoutAttributes)
            : GetVirtualLayoutFrame(virtualView, layoutAttributes);

        
        this.cachedAttributes = new WeakReference<UICollectionViewLayoutAttributes>(layoutAttributes);
        
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

    public override void PrepareForReuse()
    {
        base.PrepareForReuse();
        
        Console.WriteLine("PrepereForReuse: " + PositionInfo?.Position);

        // TODO: Recycle
        // if ((VirtualView?.TryGetTarget(out var virtualView) ?? false)
        //     && (ReuseCallback?.TryGetTarget(out var reuseCallback) ?? false))
        // {
        //     Console.WriteLine("PrepereForReuse " + virtualView);
        //     reuseCallback?.Invoke(virtualView);
        // }
    }
    
    public void SetupView(IView view)
    {
        Console.WriteLine($"SetupView {view}, Position: {PositionInfo?.Position}");
        
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
    }

    public override void SetNeedsLayout()
    {
        if (!(this.cachedAttributes?.TryGetTarget(out var layoutAttribtues) ?? false))
        {
            base.SetNeedsLayout();
            return;
        }

        var oldWidth = layoutAttribtues.Frame.Width;
        var oldHeight = layoutAttribtues.Frame.Height;
        
        // check new size
        var newAttributes = this.PreferredLayoutAttributesFittingAttributes(layoutAttribtues);
        
        // var collectionView = this.Superview as UICollectionView;
        // var layout = collectionView?.CollectionViewLayout as CvLayout;
        // layout?.InvalidateLayout();
        
        // if the content size has changed, we need to invalidate the layout
        if (newAttributes.Frame.Width != oldWidth || newAttributes.Frame.Height != oldHeight)
        {
            var collectionView = this.Superview as UICollectionView;
            var layout = collectionView?.CollectionViewLayout as CvLayout;
            layout?.InvalidateLayout();
        }
    }

    public void UpdatePosition(PositionInfo positionInfo)
    {
        PositionInfo = positionInfo;
        if (VirtualView?.TryGetTarget(out var virtualView) ?? false)
        {
            if (virtualView is IPositionInfo viewPositionInfo)
                viewPositionInfo.Update(positionInfo);
        }
    }

    private static CGRect GetHorizontalLayoutFrame(IView virtualView, UICollectionViewLayoutAttributes layoutAttributes)
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

        return new CGRect(layoutAttributes.Frame.X,
            layoutAttributes.Frame.Y,
            width,
            layoutAttributes.Frame.Width);
    }

    private static CGRect GetVirtualLayoutFrame(IView virtualView, UICollectionViewLayoutAttributes layoutAttributes)
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

        return new CGRect(layoutAttributes.Frame.X,
            layoutAttributes.Frame.Y,
            layoutAttributes.Frame.Width,
            height);
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