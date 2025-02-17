using Android.Views;
using AndroidX.RecyclerView.Widget;

namespace Microsoft.Maui;

public class RvItemHolder : RecyclerView.ViewHolder
{
	public RvViewContainer ViewContainer { get; private set; }
	public PositionInfo PositionInfo { get; private set; }

	public RvItemHolder(IMauiContext mauiContext, ListOrientation orientation)
		: base(new RvViewContainer(mauiContext)
		{
			LayoutParameters = new RecyclerView.LayoutParams(
				orientation == ListOrientation.Vertical ? ViewGroup.LayoutParams.MatchParent : ViewGroup.LayoutParams.WrapContent,
				orientation == ListOrientation.Vertical ? ViewGroup.LayoutParams.WrapContent : ViewGroup.LayoutParams.MatchParent)
		})
	{
		ViewContainer = ItemView as RvViewContainer;
	}

	public IView VirtualView
		=> ViewContainer?.VirtualView;

	public bool NeedsView
        => ViewContainer?.NeedsView ?? true;

	public void SetupView(IView view)
	{
		ViewContainer.SetupView(view);
	}

	public void UpdatePosition(PositionInfo positionInfo)
	{
		PositionInfo = positionInfo;
		ViewContainer.UpdatePosition(positionInfo);
	}
}