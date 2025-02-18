#nullable enable
using Foundation;
using Microsoft.Maui.Adapters;
using UIKit;

namespace Microsoft.Maui;

internal class CvDataSource : UICollectionViewDataSource
{
    public CvDataSource(VirtualListViewHandler handler)
        : base()
    {
        Handler = handler;
    }

    VirtualListViewHandler Handler { get; }

    readonly Dictionary<string, NSString> managedIds = new();
    nint? cachedCount;

    public IReadOnlyList<int> ItemPositionCache { get; private set; } = [];

    public int ContentHashCode { get; set; }

    public override nint NumberOfSections(UICollectionView collectionView)
        => 1;

    private NSString GetResuseId(UICollectionView collectionView, string managedId)
    {
        if (managedIds.TryGetValue(managedId, out var reuseId))
            return reuseId;

        reuseId = new NSString(managedId);
        managedIds.Add(managedId, reuseId);
        collectionView.RegisterClassForCell(typeof(CvCell),
            reuseId);
        return reuseId;
    }

    public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
    {
        var info = Handler?.PositionalViewSelector?.GetInfo(indexPath.Row);

        object? data = null;

        var nativeReuseId = CvCell.ReuseIdUnknown;

        if (info is not null)
        {
            data = Handler?.PositionalViewSelector?.Adapter?.DataFor(info.Kind, info.SectionIndex, info.ItemIndex);

            var reuseId = Handler?.PositionalViewSelector?.ViewSelector?.GetReuseId(info, data) ?? "UNKNOWN";
            nativeReuseId = this.GetResuseId(collectionView, reuseId);
        }

        var nativeCell = collectionView.DequeueReusableCell(nativeReuseId, indexPath);
        if (nativeCell is not CvCell cell)
        {
            return (UICollectionViewCell)nativeCell;
        }

        cell.SetTapHandlerCallback(TapCellHandler);
        cell.Handler = Handler;

        if (info is not null)
        {
            if (info.SectionIndex < 0 || info.ItemIndex < 0)
                info.IsSelected = false;
            else
                info.IsSelected = Handler?.IsItemSelected(info.SectionIndex, info.ItemIndex) ?? false;

            if (cell.NeedsView)
            {
                var view = Handler?.PositionalViewSelector?.ViewSelector?.CreateView(info, data);
                if (view is not null)
                    cell.SetupView(view);
            }

            cell.UpdatePosition(info);

            if (cell.VirtualView?.TryGetTarget(out var cellVirtualView) ?? false)
            {
                Handler?.PositionalViewSelector?.ViewSelector?.RecycleView(info, data, cellVirtualView);
                Handler?.VirtualView?.ViewSelector?.ViewAttached(info, cellVirtualView);
            }
        }

        return cell;
    }

    void TapCellHandler(CvCell cell)
    {
        var p = new ItemPosition(cell.PositionInfo.SectionIndex, cell.PositionInfo.ItemIndex);

        cell.PositionInfo.IsSelected = !cell.PositionInfo.IsSelected;

        if (cell.PositionInfo.IsSelected)
            Handler?.VirtualView?.SelectItem(p);
        else
            Handler?.VirtualView?.DeselectItem(p);
    }

    public void ReloadData()
    {
        cachedCount = null;
        (ItemPositionCache, ContentHashCode) = BuildItemPositionCache();
    }

    public override nint GetItemsCount(UICollectionView collectionView, nint section)
    {
        return cachedCount ??= Handler?.PositionalViewSelector?.TotalCount ?? 0;
    }

    public override bool CanMoveItem(UICollectionView collectionView, NSIndexPath indexPath)
    {
        var info = Handler?.PositionalViewSelector?.GetInfo(indexPath.Item.ToInt32());

        var adapter = Handler?.PositionalViewSelector?.Adapter as IReorderableVirtualListViewAdapter;
        return info.Kind == PositionKind.Item && adapter.CanReorderItem(info);
    }

    public override void MoveItem(UICollectionView collectionView,
                                  NSIndexPath sourceIndexPath,
                                  NSIndexPath destinationIndexPath)
    {
        var sourceInfo = Handler?.PositionalViewSelector?.GetInfo(sourceIndexPath.Item.ToInt32());
        var destinationInfo = Handler?.PositionalViewSelector?.GetInfo(destinationIndexPath.Item.ToInt32());

        var adapter = Handler?.PositionalViewSelector?.Adapter as IReorderableVirtualListViewAdapter;

        adapter.OnMoveItem(sourceInfo, destinationInfo);
    }

    private (IReadOnlyList<int> items, int contentHash) BuildItemPositionCache()
    {
        var itemHashCodes = new List<int>();
        var adapter = this.Handler.VirtualView.Adapter;
        if (adapter == null)
            return (itemHashCodes.AsReadOnly(), 0);

        var summedHash = 0;
        var numberSections = adapter.GetNumberOfSections();
        
        object? listBindingContext = null;
        var viewSelector = this.Handler.PositionalViewSelector.ViewSelector;
        if (this.Handler.VirtualView is BindableObject listView)
        {
            listBindingContext = listView.BindingContext;
        }

        if (this.Handler.PositionalViewSelector.HasGlobalHeader)
        {
            var headerHashCode = "GlobalHeader".GetHashCode();
            summedHash = HashCode.Combine(summedHash, headerHashCode);
            itemHashCodes.Add(headerHashCode);
            var resuseId = viewSelector.GetReuseIdAndView(PositionKind.Header, -1, -1, listBindingContext).reuseId;
            this.RegisterReuseId(resuseId);
        }

        for (int s = 0; s < numberSections; s++)
        {
            if (this.Handler.PositionalViewSelector.ViewSelector.SectionHasHeader(s))
            {
                var sectionHeaderHashCode = HashCode.Combine(s, "Header");
                summedHash = HashCode.Combine(summedHash, sectionHeaderHashCode);
                itemHashCodes.Add(sectionHeaderHashCode);
                var resuseId = viewSelector.GetReuseIdAndView(PositionKind.SectionHeader, s, -1, adapter.GetSection(s)).reuseId;
                this.RegisterReuseId(resuseId);
            }

            var itemsInSection = Math.Max(adapter.GetNumberOfItemsInSection(s), 0);

            for (int i = 0; i < itemsInSection; i++)
            {
                var item = adapter.GetItem(s, i);
                var itemHashCode = item.GetHashCode();
                summedHash = HashCode.Combine(summedHash, itemHashCode);
                itemHashCodes.Add(itemHashCode);
                var resuseId = viewSelector.GetReuseIdAndView(PositionKind.Item, s, i, item).reuseId;
                this.RegisterReuseId(resuseId);
            }

            if (viewSelector.SectionHasFooter(s))
            {
                var sectionFooterHashCode = HashCode.Combine(s, "Footer");
                summedHash = HashCode.Combine(summedHash, sectionFooterHashCode);
                itemHashCodes.Add(sectionFooterHashCode);
                var resuseId = viewSelector.GetReuseIdAndView(PositionKind.SectionFooter, s, -1, adapter.GetSection(s)).reuseId;
                this.RegisterReuseId(resuseId);
            }
        }

        if (this.Handler.PositionalViewSelector.HasGlobalFooter)
        {
            var footerHashCode = "GlobalFooter".GetHashCode();
            summedHash = HashCode.Combine(summedHash, footerHashCode);
            itemHashCodes.Add(footerHashCode);
            var resuseId = viewSelector.GetReuseIdAndView(PositionKind.Footer, -1, -1, listBindingContext).reuseId;
            this.RegisterReuseId(resuseId);
        }

        return (itemHashCodes.AsReadOnly(), summedHash);
    }

    private void RegisterReuseId(string managedId)
    {
        if (this.managedIds.ContainsKey(managedId))
        {
            return;
        }

        var reuseId = new NSString(managedId);
        this.managedIds.Add(managedId, reuseId);
        this.Handler.Controller.CollectionView.RegisterClassForCell(typeof(CvCell),
            reuseId);
    }
}