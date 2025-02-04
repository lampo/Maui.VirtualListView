using System.ComponentModel;
using Microsoft.Maui.Adapters;

namespace Microsoft.Maui.Controls.Controls
{
    public class ReorderableVirtualListView : VirtualListView
    {
        public event EventHandler ReorderCompleted;

        /// <summary>Bindable property for <see cref="CanMixGroups"/>.</summary>
        public static readonly BindableProperty CanMixGroupsProperty = BindableProperty.Create(nameof(CanMixGroups), typeof(bool), typeof(ReorderableItemsView), false);
        
        public bool CanMixGroups
        {
            get { return (bool)GetValue(CanMixGroupsProperty); }
            set { SetValue(CanMixGroupsProperty, value); }
        }

        /// <summary>Bindable property for <see cref="CanReorderItems"/>.</summary>
        public static readonly BindableProperty CanReorderItemsProperty = BindableProperty.Create(nameof(CanReorderItems), typeof(bool), typeof(ReorderableItemsView), false);

        public bool CanReorderItems
        {
            get { return (bool)GetValue(CanReorderItemsProperty); }
            set { SetValue(CanReorderItemsProperty, value); }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public void SendReorderCompleted() => ReorderCompleted?.Invoke(this, EventArgs.Empty);
    }
}
