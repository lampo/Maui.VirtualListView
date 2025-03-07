using System.Diagnostics;
using Android.Content;
using Android.Views;
using AndroidX.RecyclerView.Widget;
using Microsoft.Maui.Adapters;
using static Android.Icu.Text.IDNA;

namespace Microsoft.Maui;

public partial class RvAdapter : RecyclerView.Adapter
{
    readonly VirtualListViewHandler handler;

    readonly object lockObj = new object();

    readonly PositionalViewSelector positionalViewSelector;
    private readonly RecyclerView.RecycledViewPool recycledViewPool;
    private readonly int itemMaxRecyclerViews;

    RvViewHolderClickListener clickListener;

    public Context Context { get; }

    int? cachedItemCount = null;

    public override int ItemCount
        => positionInfoCache.Count;

    private List<List<object>> items = [];
    private List<PositionInfo> positionInfoCache = [];
    

    public RvAdapter(Context context,
                       VirtualListViewHandler handler,
                       PositionalViewSelector positionalViewSelector,
                       RecyclerView.RecycledViewPool recycledViewPool,
                       int itemMaxRecyclerViews)
    {
        Context = context;
        HasStableIds = false;

        this.handler = handler;
        this.positionalViewSelector = positionalViewSelector;
        this.recycledViewPool = recycledViewPool;
        this.itemMaxRecyclerViews = itemMaxRecyclerViews;

        if (positionalViewSelector?.Adapter == null)
        {
            return;
        }

        for (int s = 0; s < positionalViewSelector?.Adapter?.GetNumberOfSections(); s++)
        {
            var section = new List<object>();
            for (int i = 0; i < positionalViewSelector.Adapter.GetNumberOfItemsInSection(s); i++)
            {
                section.Add(positionalViewSelector.Adapter.GetItem(s, i));
            }
            items.Add(section);
        }
    }

    public float DisplayScale =>
        handler?.Context?.Resources.DisplayMetrics.Density ?? 1;

    public override void OnViewAttachedToWindow(Java.Lang.Object holder)
    {
        base.OnViewAttachedToWindow(holder);

        if (holder is RvItemHolder rvItemHolder && rvItemHolder?.ViewContainer?.VirtualView != null)
            handler.VirtualView.ViewSelector.ViewAttached(rvItemHolder.PositionInfo, rvItemHolder.ViewContainer.VirtualView);
    }

    public override void OnViewDetachedFromWindow(Java.Lang.Object holder)
    {
        if (holder is RvItemHolder rvItemHolder && rvItemHolder?.ViewContainer?.VirtualView != null)
        {

            handler.VirtualView.ViewSelector.ViewDetached(rvItemHolder.PositionInfo, rvItemHolder.ViewContainer.VirtualView);
        }

        base.OnViewDetachedFromWindow(holder);
    }

    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
    {
        var info = positionInfoCache.Count > position ? positionInfoCache[position] : null;
        if (info == null)
            return;

        // The template selector doesn't infer selected properly
        // so we need to ask the listview which tracks selections about the state
        info.IsSelected = info.Kind == PositionKind.Item
            && (handler?.IsItemSelected(info.SectionIndex, info.ItemIndex) ?? false);

        if (holder is RvItemHolder itemHolder)
        {
            var data = info.Kind switch
            {
                PositionKind.Item =>
                    items[info.SectionIndex][info.ItemIndex],
                PositionKind.SectionHeader =>
                    positionalViewSelector?.Adapter?.GetSection(info.SectionIndex),
                PositionKind.SectionFooter =>
                    positionalViewSelector?.Adapter?.GetSection(info.SectionIndex),
                _ => null
            };

            itemHolder.UpdatePosition(info);
            positionalViewSelector?.ViewSelector?.RecycleView(info, data, itemHolder.ViewContainer.VirtualView);
        }
    }

    private readonly Dictionary<string, int> cachedReuseIds = new();
    private readonly Dictionary<int, object> cachedViews = new();
    private int reuseIdCount = 100;

    public override int GetItemViewType(int position)
    {
        var info = positionInfoCache[position];

        var data = info.Kind switch
        {
            PositionKind.Item =>
                items[info.SectionIndex][info.ItemIndex],
            PositionKind.SectionHeader =>
                positionalViewSelector?.Adapter?.GetSection(info.SectionIndex),
            PositionKind.SectionFooter =>
                positionalViewSelector?.Adapter?.GetSection(info.SectionIndex),
            _ => null
        };

        var (reuseId, view) = positionalViewSelector.ViewSelector.GetReuseIdAndView(info, data);

        int vt = -1;

        lock (lockObj)
        {
            if (!cachedReuseIds.TryGetValue(reuseId, out var reuseIdNumber))
            {
                reuseIdNumber = ++reuseIdCount;
                cachedReuseIds.Add(reuseId, reuseIdNumber);
                this.cachedViews.Add(reuseIdNumber, view);
                var resusePoolCount = info.Kind switch
                {
                    PositionKind.Header => 1,
                    PositionKind.Item => itemMaxRecyclerViews,
                    PositionKind.Footer => 1,
                    _ => 5
                };
                recycledViewPool.SetMaxRecycledViews(reuseIdNumber, resusePoolCount);
            }

            vt = reuseIdNumber;
        }

        return vt;
    }

    public override long GetItemId(int position) => RecyclerView.NoId;

    public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
    {
        Debug.WriteLine("OnCreateViewHolder");
        var viewHolder = new RvItemHolder(handler.MauiContext, handler.VirtualView.Orientation);

        clickListener = new RvViewHolderClickListener(viewHolder, rvh =>
        {
            if (rvh.PositionInfo == null || rvh.PositionInfo.Kind != PositionKind.Item)
                return;

            var p = new ItemPosition(rvh.PositionInfo.SectionIndex, rvh.PositionInfo.ItemIndex);

            rvh.PositionInfo.IsSelected = !rvh.PositionInfo.IsSelected;

            if (rvh.VirtualView is IPositionInfo positionInfo)
                positionInfo.IsSelected = rvh.PositionInfo.IsSelected;

            if (rvh.PositionInfo.IsSelected)
                handler?.VirtualView?.SelectItem(p);
            else
                handler?.VirtualView?.DeselectItem(p);
        });

        viewHolder.ItemView.SetOnClickListener(clickListener);

        var viewOrTemplate = this.cachedViews[viewType];
        viewHolder.SetupView(CreateContent(viewOrTemplate));

        return viewHolder;
    }

    private bool suspendNotifications = false;

    public void InvalidateData()
    {
        cachedItemCount = null;
        if (suspendNotifications)
            return;

        items.Clear();
        positionInfoCache.Clear();
        if (positionalViewSelector?.Adapter == null)
        {
            this.NotifyDataSetChanged();
            return;
        }

        for (int s = 0; s < positionalViewSelector?.Adapter?.GetNumberOfSections(); s++)
        {
            var section = new List<object>();
            for (int i = 0; i < positionalViewSelector.Adapter.GetNumberOfItemsInSection(s); i++)
            {
                section.Add(positionalViewSelector.Adapter.GetItem(s, i));
            }
            items.Add(section);
        }
        
        for (int s = 0; s < positionalViewSelector.TotalCount; s++)
        {
            var info = positionalViewSelector.GetInfo(s);
            positionInfoCache.Add(info);
        }

        this.NotifyDataSetChanged();
    }

    private static IView? CreateContent(object viewOrTemplate)
    {
        if (viewOrTemplate is DataTemplate template)
        {
            return template.CreateContent() as IView;
        }
        return viewOrTemplate as IView;
    }

    public bool OnItemMove(int fromPositionIndex, int toPositionIndex)
    {
        var fromPosition = positionInfoCache.Count > fromPositionIndex ? positionInfoCache[fromPositionIndex] : null;
        var toPosition = positionInfoCache.Count > toPositionIndex ? positionInfoCache[toPositionIndex] : null;

        if (fromPosition?.Kind != toPosition?.Kind)
        {
            return false;
        }
        suspendNotifications = true;

        if (!((IReorderableVirtualListViewAdapter)handler.VirtualView.Adapter).CanMoveItem(fromPosition, toPosition))
        {
            return false;
        }


        var item = items[fromPosition.SectionIndex][fromPosition.ItemIndex];
        items[fromPosition.SectionIndex].RemoveAt(fromPosition.ItemIndex);
        items[toPosition.SectionIndex].Insert(toPosition.ItemIndex, item);

        this.NotifyItemMoved(fromPositionIndex, toPositionIndex);

        return true;
    }

    public bool CanReorderItem(PositionInfo position)
    {
        return ((IReorderableVirtualListViewAdapter)handler.VirtualView.Adapter).CanReorderItem(position);
    }

    public void OnDrop(RvItemHolder itemHolder)
    {
        var info = positionInfoCache[itemHolder.AbsoluteAdapterPosition];

        suspendNotifications = false;        
        ((IReorderableVirtualListViewAdapter)handler.VirtualView.Adapter).OnReorderComplete(itemHolder.PositionInfo.SectionIndex, itemHolder.PositionInfo.ItemIndex, info.SectionIndex, info.ItemIndex);
    }
}