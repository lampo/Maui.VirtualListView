using Microsoft.Maui.Adapters;

namespace Microsoft.Maui;

public interface IVirtualListView : IView
{
	IVirtualListViewAdapter Adapter { get; }

	IVirtualListViewSelector ViewSelector { get; }

	IView Header { get; }

	bool IsHeaderVisible { get; }

	IView Footer { get; }

	bool IsFooterVisible { get; }

	event EventHandler<ScrolledEventArgs> OnScrolled;

	void Scrolled(double x, double y);

	SelectionMode SelectionMode { get; }

	IList<ItemPosition> SelectedItems { get; set; }

	ItemPosition? SelectedItem { get; set; }
	
	ScrollBarVisibility VerticalScrollbarVisibility { get; set; }
	
	ScrollBarVisibility HorizontalScrollbarVisibility { get; set; }

	event EventHandler<SelectedItemsChangedEventArgs> OnSelectedItemsChanged;

	Color RefreshAccentColor { get; }

	void Refresh(Action completionCallback);

	bool IsRefreshEnabled { get; }
	
	ListOrientation Orientation { get; }

	IView EmptyView { get; }

	void SelectItem(ItemPosition path);

	void DeselectItem(ItemPosition path);

	void ClearSelectedItems();

	void ScrollToItem(ItemPosition path, bool animated);

	IReadOnlyList<IPositionInfo> FindVisiblePositions();	
}

public interface IReorderbleVirtualListView : IVirtualListView
{
    bool CanReorderItems { get; set; }
    
    new IReorderableVirtualListViewAdapter Adapter { get; set; }
}

public enum ListOrientation
{
	Vertical,
	Horizontal,
}
