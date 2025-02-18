using Foundation;
using UIKit;

namespace Microsoft.Maui;

internal class ReusableIdManager
{
	public ReusableIdManager(string uniquePrefix, NSString supplementaryKind = null)
	{
		UniquePrefix = uniquePrefix;
		SupplementaryKind = supplementaryKind;
		managedIds = new Dictionary<string, NSString>();
		lockObj = new object();
	}

	public string UniquePrefix { get; }
	public NSString SupplementaryKind { get; }

	readonly Dictionary<string, NSString> managedIds;
	readonly object lockObj;

	NSString GetReuseId(int i, string idModifier = null)
		=> new NSString($"_{UniquePrefix}_VirtualListView_{i}");

	public NSString GetReuseId(UICollectionView collectionView, string managedId)
	{
		Console.WriteLine("Managed Reuse Id: " + managedId);
		var viewType = 0;

		lock (lockObj)
		{
			if (this.managedIds.TryGetValue(managedId, out var reuseId))
			{
				return reuseId;
			}

			reuseId = new NSString(managedId);
			this.managedIds.Add(managedId, reuseId);
			collectionView.RegisterClassForCell(
					typeof(CvCell),
					reuseId);
			return reuseId;

			// viewType = managedIds.IndexOf(managedId);
			//
			// if (viewType < 0)
			// {
			// 	managedIds.Add(managedId);
			// 	viewType = managedIds.Count - 1;
			//
			// 	collectionView.RegisterClassForCell(
			// 		typeof(CvCell),
			// 		GetReuseId(viewType));
			// 	Console.WriteLine("Registering ReuseId: " + GetReuseId(viewType));
			// }
		}
		
		var resueId = GetReuseId(viewType);
		
		Console.WriteLine("found Reuse: " + GetReuseId(viewType));
		
		return resueId;
	}

	public void ResetTemplates(UICollectionView collectionView)
	{
		Console.WriteLine("Reset Templates");
		lock (lockObj)
		{
			foreach (var resuseId in managedIds.Values)
			{
				collectionView.RegisterClassForCell(null, resuseId);
			}

			managedIds.Clear();
		}
	}
}
