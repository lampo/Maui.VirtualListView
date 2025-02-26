using AndroidX.RecyclerView.Widget;
using AndroidX.SwipeRefreshLayout.Widget;

namespace Microsoft.Maui.Controls.Platforms.Android
{
    internal class RvItemTouchHelperCallback : ItemTouchHelper.Callback
    {
        private const float DragElevation = 20f;

        RvAdapter adapter;
        SwipeRefreshLayout swipeRefreshLayout;

        public RvItemTouchHelperCallback(RvAdapter adapter, SwipeRefreshLayout swipeRefreshLayout)
        {
            this.adapter = adapter;
            this.swipeRefreshLayout = swipeRefreshLayout;
        }

        public override bool IsLongPressDragEnabled => true;

        public override int GetMovementFlags(RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder)
        {
            var rvHolder = viewHolder as RvItemHolder;

            var canMove = this.adapter.CanReorderItem(rvHolder.PositionInfo);

            if (!canMove)
            {
                return MakeMovementFlags(0, 0);
            }

            var itemViewType = rvHolder.PositionInfo.Kind;
            if (itemViewType is PositionKind.Header or PositionKind.Footer or PositionKind.SectionHeader or PositionKind.SectionFooter)
            {
                return MakeMovementFlags(0, 0);
            }

            var dragFlags = ItemTouchHelper.Up | ItemTouchHelper.Down;
            return MakeMovementFlags(dragFlags, 0);
        }

        public override bool OnMove(RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder, RecyclerView.ViewHolder target) =>
            this.adapter.OnItemMove(viewHolder.BindingAdapterPosition, target.BindingAdapterPosition);

        public override void OnSwiped(RecyclerView.ViewHolder viewHolder, int direction)
        {
        }

        public override void ClearView(RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder)
        {
            swipeRefreshLayout.Enabled = true; // Re-enable pull-to-refresh
            base.ClearView(recyclerView, viewHolder);

            var rvHolder = viewHolder as RvItemHolder;
            rvHolder.ItemView.Animate()
                      .ScaleX(1f)
                      .ScaleY(1f)
                      .SetDuration(100)
                      .Start();
            this.adapter.OnDrop(rvHolder);
        }

        public override void OnSelectedChanged(RecyclerView.ViewHolder viewHolder, int actionState)
        {
            base.OnSelectedChanged(viewHolder, actionState);
            if (actionState == ItemTouchHelper.ActionStateDrag)
            {
                swipeRefreshLayout.Enabled = false; // Disable pull-to-refresh
                // Scale the view to be bigger when the drag starts
                viewHolder.ItemView.Animate()
                          .ScaleX(1.05f)
                          .ScaleY(1.05f)
                          .SetDuration(100)
                          .Start();
            }
        }
    }
}
