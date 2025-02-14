using Microsoft.Maui.Controls.Platforms.Android;

namespace Microsoft.Maui;

public partial class ReordableVirtualListViewHandler
{
    private ItemTouchHelper? itemTouchHelper;

    public static void MapCanReorderItems(ReordableVirtualListViewHandler handler,
                                          IReorderbleVirtualListView virtualListView)
    {
        if (virtualListView.CanReorderItems)
        {
            if (handler.itemTouchHelper is null)
            {
                handler.itemTouchHelper =
                    new ItemTouchHelper(new RvItemTouchHelperCallback(handler.adapter, handler.swipeRefreshLayout));
                handler.itemTouchHelper.AttachToRecyclerView(handler.recyclerView);
            }
        }
        else
        {
            handler.itemTouchHelper?.AttachToRecyclerView(null);
            handler.itemTouchHelper?.Dispose();
            handler.itemTouchHelper = null;
        }
		
    }
}