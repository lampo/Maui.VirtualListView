using Android.Views;
using Microsoft.Maui.Platform;
using AView = Android.Views.View;

namespace Microsoft.Maui;

sealed class RvViewContainer : Android.Widget.FrameLayout
{
    public RvViewContainer(IMauiContext context)
        : base(context.Context ?? throw new ArgumentNullException($"{nameof(context.Context)}"))
    {
        MauiContext = context;
        Id = AView.GenerateViewId();;
    }

    public readonly IMauiContext MauiContext;

    public IView VirtualView { get; private set; }

    public AView NativeView { get; private set; }

    public bool NeedsView =>
        VirtualView is null || VirtualView.Handler is null || NativeView is null;

    public void UpdatePosition(IPositionInfo positionInfo)
    {
        if (VirtualView is IPositionInfo viewWithPositionInfo)
            viewWithPositionInfo.Update(positionInfo);
    }

    public void SetupView(IView view)
    {
        if (NativeView is null)
        {
            NativeView = view.ToPlatform(MauiContext);
            
            if (NativeView.Parent is ViewGroup parent)
            {
                parent.RemoveView(NativeView);
            }

            AddView(NativeView);
        }

        if (VirtualView is null)
        {
            VirtualView = view;
        }
    }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        if (NativeView != null)
        {
            MeasureChild(NativeView, widthMeasureSpec, heightMeasureSpec);
            SetMeasuredDimension(
                NativeView.MeasuredWidth,
                NativeView.MeasuredHeight
            );
        }
        else
        {
            base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
        }
    }

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        if (NativeView != null)
        {
            NativeView.Layout(0, 0, right - left, bottom - top);
        }
        else
        {
            base.OnLayout(changed, left, top, right, bottom);
        }
    }


    protected override void OnAttachedToWindow()
    {
        if (this.ChildCount == 0)
        {
            if (NativeView.Parent is ViewGroup parent)
            {
                parent.RemoveView(NativeView);
            }
            AddView(NativeView);
        }

        base.OnAttachedToWindow();
    }
}
