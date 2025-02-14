using Microsoft.Maui.Adapters;

namespace Microsoft.Maui.Controls.Controls
{
    public class ReorderbleVirtualListView : VirtualListView, IReorderbleVirtualListView
    {
        /// <summary>Bindable property for <see cref="CanReorderItems"/>.</summary>
        public static readonly BindableProperty CanReorderItemsProperty =
            BindableProperty.Create(nameof(CanReorderItems), typeof(bool), typeof(ReorderableItemsView), false);

        /// <summary>
        /// Controls whether the user can reorder items in the list by dragging and dropping them.
        /// </summary>
        public bool CanReorderItems
        {
            get { return (bool)GetValue(CanReorderItemsProperty); }
            set { SetValue(CanReorderItemsProperty, value); }
        }

        public IReorderableVirtualListViewAdapter Adapter
        {
            get => (IReorderableVirtualListViewAdapter)base.Adapter;
            set => base.Adapter = value;
        }
    }
}