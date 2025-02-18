using CoreGraphics;
using Foundation;
using ObjCRuntime;
using UIKit;

namespace Microsoft.Maui;

public class CvCollectionView : UICollectionView
{
    public CvCollectionView(NSCoder coder)
        : base(coder)
    {
    }

    protected CvCollectionView(NSObjectFlag t)
        : base(t)
    {
    }

    internal protected CvCollectionView(NativeHandle handle)
        : base(handle)
    {
    }

    public CvCollectionView(CGRect frame, UICollectionViewLayout layout)
        : base(frame, layout)
    {
    }
    
    
}