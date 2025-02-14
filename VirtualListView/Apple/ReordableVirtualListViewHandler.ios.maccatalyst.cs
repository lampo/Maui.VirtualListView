#nullable enable

namespace Microsoft.Maui;

public partial class ReordableVirtualListViewHandler
{
    public static void MapCanReorderItems(ReordableVirtualListViewHandler handler,
                                          IReorderbleVirtualListView virtualListView)
    {
        handler.Controller.CollectionView.DragInteractionEnabled = virtualListView.CanReorderItems;
    }

    protected override VirtualListViewController CreateController() => new ReorderbleVirtualListViewController(this);

    protected virtual bool SuspendInvalidateUpdate() => ((ReorderbleVirtualListViewController)Controller)
        .IsDragging;
}