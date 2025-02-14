namespace Microsoft.Maui;

public partial class ReordableVirtualListViewHandler : VirtualListViewHandler
{
    public ReordableVirtualListViewHandler() : base(ViewMapper)
    {

    }
    
    public ReordableVirtualListViewHandler(IPropertyMapper mapper = null) : base(mapper ?? ViewMapper)
    {

    }
    
    #if ANDROID || IOS || MACCATALYST
    public new static readonly IPropertyMapper<IReorderbleVirtualListView, ReordableVirtualListViewHandler> ViewMapper = new PropertyMapper<IReorderbleVirtualListView, ReordableVirtualListViewHandler>(VirtualListViewHandler.ViewMapper)
    {
        [nameof(IReorderbleVirtualListView.CanReorderItems)] = MapCanReorderItems,
    };
    #endif
}