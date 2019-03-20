using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Extensions
{
	public static class LinqExtension
	{
		public static bool AllEqual<T>(this IEnumerable<T> values)
		{
			if (!values.Any()) return true;

			var first = values.First();
			return values.Skip(1).All(v => first.Equals(v));
		}
	}
}
