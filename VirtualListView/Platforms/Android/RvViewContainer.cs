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
        Id = AView.GenerateViewId();
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
            try
            {
                if (NativeView.Parent is ViewGroup parent)
                {
                    parent.RemoveView(NativeView);
                }

                AddView(NativeView);
            }
            catch (Java.Lang.IllegalStateException e)
            {
                var breadcrumbData = new Dictionary<string, object>
                {
                    { "ViewType", view.GetType().FullName },
                    { "ViewId", NativeView.Id },
                    { "VirtualViewType", VirtualView?.GetType().FullName ?? "null" },
                    { "ParentViewType", NativeView.Parent?.GetType().FullName ?? "null" },
                    { "ParentViewId", (NativeView.Parent as AView)?.Id ?? -1 },
                    { "ContainerId", this.Id },
                    { "Context", MauiContext.Context?.ToString() ?? "null" }
                };

                Bugsnag.Maui.BugsnagMaui.Current?.LeaveBreadcrumb(
                    "IllegalStateException in RvViewContainer.SetupView",
                    breadcrumbData
                );
                Bugsnag.Maui.BugsnagMaui.Current?.Notify(e);
                if (NativeView.Parent is ViewGroup parent)
                {
                    parent.RemoveView(NativeView);
                }

                AddView(NativeView);
            }
        }

        if (VirtualView is null)
        {
            VirtualView = view;
        }
    }
}
