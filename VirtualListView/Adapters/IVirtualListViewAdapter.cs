namespace Microsoft.Maui.Adapters;

public interface IVirtualListViewAdapter
{
	int GetNumberOfSections();

	object GetSection(int sectionIndex);

	int GetNumberOfItemsInSection(int sectionIndex);

	object GetItem(int sectionIndex, int itemIndex);

	event EventHandler OnDataInvalidated;

	void InvalidateData();	   
}


public interface IReorderableVirtualListViewAdapter : IVirtualListViewAdapter
{
    bool OnMoveItem(PositionInfo from, PositionInfo to);

	void OnReorderComplete(int originalSection, int originalIndex, int finalSection, int finalIndex);

    bool CanReorderItem(PositionInfo position);
}