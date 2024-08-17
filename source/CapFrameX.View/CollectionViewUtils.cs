using System;
using System.Linq;
using System.Windows.Data;

namespace CapFrameX.View
{
    public static class CollectionViewUtils
    {
		public static void FilterCollectionByText<T>(this FilterEventArgs args, string text, Func<T, string, bool> containingChecker)
		{
			if (!(args.Item is T))
			{
				args.Accepted = false;
				return;
			}

			var itemCast = (T)args.Item;
			var words = text.Split(' ').Where(w => !string.IsNullOrWhiteSpace(w)).ToArray();
			args.Accepted = words.All(w => containingChecker(itemCast, w));
		}
	}
}
