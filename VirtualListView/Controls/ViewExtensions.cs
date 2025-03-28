using Microsoft.Maui.Controls.Internals;

namespace Microsoft.Maui.Controls;

internal static class ViewExtensions
{
	internal static string GetDataTemplateId(this DataTemplate dataTemplate)
	{
			
		return ((IDataTemplateController)dataTemplate)?.IdString;

	}

	internal static void RemoveLogicalChild(this Element parent, IView view)
	{
		if (view is Element elem)
		{
            parent.RemoveLogicalChild(elem);
		}
	}

	internal static void AddLogicalChild(this Element parent, IView view)
	{
		if (view is Element elem)
		{
            parent.AddLogicalChild(elem);
        }
	}
}

