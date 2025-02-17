using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using AndroidX.SwipeRefreshLayout.Widget;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace Microsoft.Maui;

public partial class VirtualListViewHandler : ViewHandler<IVirtualListView, FrameLayout>
{
    /// <summary>
    /// this is the maximum number of RecyclerViews that will be cached by the RecyclerViewPool, only adjust for know lists to improve performance.
	/// This value will be used to set the RecyclerView.SetItemViewCacheSize method for each individual template type.
	/// The balance is to have enough RecyclerViews to avoid creating them on the fly, but not too many to avoid memory issues.
    /// </summary>
    protected virtual int ItemMaxRecyclerViews => 10;

    FrameLayout rootLayout;
	protected SwipeRefreshLayout swipeRefreshLayout;
	protected RvAdapter adapter;
	protected RecyclerView recyclerView;
	LinearLayoutManager layoutManager;
	Android.Views.View emptyView;

	protected override FrameLayout CreatePlatformView()
	{
		rootLayout ??= new FrameLayout(Context);
        recyclerView ??= CreateRecyclerView();

        if (swipeRefreshLayout is null)
		{
			swipeRefreshLayout = CreateSwipeRefreshLayout();
			swipeRefreshLayout.AddView(recyclerView, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
		}

		rootLayout.AddView(swipeRefreshLayout, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

		return rootLayout;
	}

	protected virtual RecyclerView CreateRecyclerView() => new(Context);

	protected virtual SwipeRefreshLayout CreateSwipeRefreshLayout() => new(Context);

    protected override void ConnectHandler(FrameLayout nativeView)
	{
		swipeRefreshLayout.SetOnRefreshListener(new SrlRefreshListener(() =>
		{
			VirtualView?.Refresh(() => swipeRefreshLayout.Refreshing = false);
		}));
		
		layoutManager = new LinearLayoutManager(Context);
		//layoutManager.Orientation = LinearLayoutManager.Horizontal;

		PositionalViewSelector = new PositionalViewSelector(VirtualView);

		adapter = new RvAdapter(Context, this, PositionalViewSelector, recyclerView.GetRecycledViewPool(), ItemMaxRecyclerViews);
		
        recyclerView.NestedScrollingEnabled = false;

		recyclerView.AddOnScrollListener(new RvScrollListener((rv, dx, dy) =>
		{
			var x = Context.FromPixels(dx);
			var y = Context.FromPixels(dy);
			
			VirtualView?.Scrolled(x, y);
		}));
		
        recyclerView.SetLayoutManager(layoutManager);
		recyclerView.SetAdapter(adapter);
        recyclerView.LayoutParameters = new ViewGroup.LayoutParams(
			ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
	}

	protected override void DisconnectHandler(FrameLayout nativeView)
	{
		recyclerView.ClearOnScrollListeners();
		recyclerView.SetAdapter(null);
		adapter.Dispose();
		adapter = null;
		layoutManager.Dispose();
		layoutManager = null;
	}

	public void InvalidateData()
	{
		int originalFirstVisibleItemPosition = layoutManager.FindFirstVisibleItemPosition();
        int positionDelta = GetCurrentScrollPositionDelta();
		UpdateEmptyViewVisibility();

		recyclerView.Post(() => {
			adapter?.InvalidateData();			
			
            var avialablePositions = GetNumberOfAvailableScrollPositions();
            
			if (originalFirstVisibleItemPosition > avialablePositions)
			{
                recyclerView.ScrollToPosition(avialablePositions - positionDelta);
			}            
        });
	}

    private int GetCurrentScrollPositionDelta()
    {
        var avialablePositions = GetNumberOfAvailableScrollPositions();
        return avialablePositions - layoutManager.FindFirstVisibleItemPosition();
    }

    private int GetNumberOfAvailableScrollPositions()
    {
        return adapter?.ItemCount ?? 0;
    }

	void PlatformScrollToItem(ItemPosition itemPosition, bool animated)
	{
		var position = PositionalViewSelector.GetPosition(itemPosition.SectionIndex, itemPosition.ItemIndex);

		recyclerView.ScrollToPosition(position);
	}

	public static void MapHeader(VirtualListViewHandler handler, IVirtualListView virtualListView)
		=> handler.InvalidateData();

	public static void MapFooter(VirtualListViewHandler handler, IVirtualListView virtualListView)
		=> handler.InvalidateData();

	public static void MapViewSelector(VirtualListViewHandler handler, IVirtualListView virtualListView)
		=> handler.InvalidateData();

	public static void MapSelectionMode(VirtualListViewHandler handler, IVirtualListView virtualListView)
	{ }

	public static void MapInvalidateData(VirtualListViewHandler handler, IVirtualListView virtualListView, object parameter)
		=> handler.InvalidateData();

	void PlatformUpdateItemSelection(ItemPosition itemPosition, bool selected)
	{
		var position = PositionalViewSelector.GetPosition(itemPosition.SectionIndex, itemPosition.ItemIndex);

		var vh = recyclerView.FindViewHolderForAdapterPosition(position);

		if (vh is RvItemHolder rvh)
		{
			rvh.PositionInfo.IsSelected = selected;

			if (rvh.ViewContainer?.VirtualView is IPositionInfo viewPositionInfo)
				viewPositionInfo.IsSelected = selected;
		}
	}

	public static void MapOrientation(VirtualListViewHandler handler, IVirtualListView virtualListView)
	{
		handler.layoutManager.Orientation = virtualListView.Orientation switch
		{
			ListOrientation.Vertical => LinearLayoutManager.Vertical,
			ListOrientation.Horizontal => LinearLayoutManager.Horizontal,
			_ => LinearLayoutManager.Vertical
		};
		handler.InvalidateData();
	}

	public static void MapRefreshAccentColor(VirtualListViewHandler handler, IVirtualListView virtualListView)
	{
		if (virtualListView.RefreshAccentColor is not null)
			handler.swipeRefreshLayout.SetColorSchemeColors(virtualListView.RefreshAccentColor.ToPlatform());
	}

	public static void MapIsRefreshEnabled(VirtualListViewHandler handler, IVirtualListView virtualListView)
	{
		handler.swipeRefreshLayout.Enabled = virtualListView.IsRefreshEnabled;
	}

	public static void MapEmptyView(VirtualListViewHandler handler, IVirtualListView virtualListView)
	{
		handler?.UpdateEmptyView();
	}

	void UpdateEmptyViewVisibility()
	{
		if (emptyView is not null)
			emptyView.Visibility = ShouldShowEmptyView ? ViewStates.Visible : ViewStates.Gone;
	}

	void UpdateEmptyView()
	{
		if (emptyView is not null)
		{
			emptyView.RemoveFromParent();
			emptyView.Dispose();
		}

		emptyView = VirtualView?.EmptyView?.ToPlatform(MauiContext);

		if (emptyView is not null)
		{
			this.rootLayout.AddView(emptyView, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
			UpdateEmptyViewVisibility();
		}
	}
	
	ScrollBarVisibility _defaultHorizontalScrollVisibility = ScrollBarVisibility.Default;
	ScrollBarVisibility _defaultVerticalScrollVisibility = ScrollBarVisibility.Default;

	void UpdateVerticalScrollbarVisibility(ScrollBarVisibility scrollBarVisibility)
	{
		if (_defaultVerticalScrollVisibility == ScrollBarVisibility.Default)
			_defaultVerticalScrollVisibility =
				recyclerView.VerticalScrollBarEnabled ? ScrollBarVisibility.Always : ScrollBarVisibility.Never;

		var newVerticalScrollVisiblility = scrollBarVisibility;

		if (newVerticalScrollVisiblility == ScrollBarVisibility.Default)
			newVerticalScrollVisiblility = _defaultVerticalScrollVisibility;

		recyclerView.VerticalScrollBarEnabled = newVerticalScrollVisiblility == ScrollBarVisibility.Always;
	}
	
	void UpdateHorizontalScrollbarVisibility(ScrollBarVisibility scrollBarVisibility)
	{
		if (_defaultHorizontalScrollVisibility == ScrollBarVisibility.Default)
			_defaultHorizontalScrollVisibility =
				recyclerView.HorizontalScrollBarEnabled ? ScrollBarVisibility.Always : ScrollBarVisibility.Never;

		var newHorizontalScrollVisiblility = scrollBarVisibility;

		if (newHorizontalScrollVisiblility == ScrollBarVisibility.Default)
			newHorizontalScrollVisiblility = _defaultHorizontalScrollVisibility;

		recyclerView.HorizontalScrollBarEnabled = newHorizontalScrollVisiblility == ScrollBarVisibility.Always;
	}
	
	public IReadOnlyList<IPositionInfo> FindVisiblePositions()
	{
		var positions = new List<IPositionInfo>();
		
		var firstVisibleItemPosition = layoutManager.FindFirstVisibleItemPosition();
		var lastVisibleItemPosition = layoutManager.FindLastVisibleItemPosition();

		for (var p = firstVisibleItemPosition; p <= lastVisibleItemPosition; p++)
		{
			positions.Add(PositionalViewSelector.GetInfo(p));
		}

		return positions;
	}
}