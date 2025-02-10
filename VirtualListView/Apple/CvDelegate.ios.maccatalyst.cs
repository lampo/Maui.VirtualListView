﻿using System.Runtime.InteropServices;
using CoreGraphics;
using Foundation;
using UIKit;

namespace Microsoft.Maui;

internal class CvDelegate : UICollectionViewDelegateFlowLayout
{
	public CvDelegate(VirtualListViewHandler handler, VirtualListViewController viewController)
		: base()
	{
		Handler = handler;
        this.viewController = viewController;
		var collectionView = viewController.CollectionView;
        NativeCollectionView = new WeakReference<UICollectionView>(collectionView);
		collectionView.RegisterClassForCell(typeof(CvCell), CvCell.ReuseIdUnknown);
	}

	internal readonly WeakReference<UICollectionView> NativeCollectionView;
	internal readonly VirtualListViewHandler Handler;
    private readonly VirtualListViewController viewController;

    public Action<NFloat, NFloat> ScrollHandler { get; set; }
    
	public override void ItemSelected(UICollectionView collectionView, NSIndexPath indexPath)
		=> HandleSelection(collectionView, indexPath, true);

	public override void ItemDeselected(UICollectionView collectionView, NSIndexPath indexPath)
		=> HandleSelection(collectionView, indexPath, false);

	void HandleSelection(UICollectionView collectionView, NSIndexPath indexPath, bool selected)
	{
		//UIView.AnimationsEnabled = false;
		if (collectionView.CellForItem(indexPath) is CvCell selectedCell
		    && (selectedCell.PositionInfo?.Kind ?? PositionKind.Header) == PositionKind.Item)
		{
			selectedCell.UpdateSelected(selected);

			if (selectedCell.PositionInfo is not null)
			{
				var itemPos = new ItemPosition(
					selectedCell.PositionInfo.SectionIndex,
					selectedCell.PositionInfo.ItemIndex);

				if (selected)
					Handler?.VirtualView?.SelectItem(itemPos);
				else
					Handler?.VirtualView?.DeselectItem(itemPos);
			}
		}
	}

	public override void Scrolled(UIScrollView scrollView)
	{
		ScrollHandler?.Invoke(scrollView.ContentOffset.X, scrollView.ContentOffset.Y);
	}

	public override bool ShouldSelectItem(UICollectionView collectionView, NSIndexPath indexPath)
		=> IsRealItem(indexPath);

	public override bool ShouldDeselectItem(UICollectionView collectionView, NSIndexPath indexPath)
		=> IsRealItem(indexPath);

	bool IsRealItem(NSIndexPath indexPath)
	{
		var info = Handler?.PositionalViewSelector?.GetInfo(indexPath.Item.ToInt32());
		return (info?.Kind ?? PositionKind.Header) == PositionKind.Item;
	}

    public override NSIndexPath GetTargetIndexPathForMove(UICollectionView collectionView, NSIndexPath originalIndexPath, NSIndexPath proposedIndexPath)
    {
		Console.WriteLine("GetTargetIndexPathForMove");
        NSIndexPath targetIndexPath;
        // var layout = (CvLayout)collectionView.CollectionViewLayout;
        // layout.GetTargetIndexPathForMove(originalIndexPath, proposedIndexPath);
		return proposedIndexPath;
        //var itemsView = viewController?.Item;
        //if (itemsView?.IsGrouped == true)
        //{
        //    if (originalIndexPath.Section == proposedIndexPath.Section || itemsView.CanMixGroups)
        //    {
        //        targetIndexPath = proposedIndexPath;
        //    }
        //    else
        //    {
        //        targetIndexPath = originalIndexPath;
        //    }
        //}
        //else
        //{
        //    targetIndexPath = proposedIndexPath;
        //}

        //return targetIndexPath;
    }

  //   public override CGSize GetSizeForItem(UICollectionView collectionView, UICollectionViewLayout layout, NSIndexPath indexPath)
  //   {
	 //    
	 //    var size = base.GetSizeForItem(collectionView, layout, indexPath);
		// Console.WriteLine($"GetSizeForItem: Path: {indexPath}" + size);
	 //    return size;
  //   }
}
