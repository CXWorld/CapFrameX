using System.ComponentModel;

namespace CapFrameX.Extensions.Attributes
{
	public sealed class ShortDescriptionAttribute : DescriptionAttribute
	{
		public ShortDescriptionAttribute()
			: base() { }

		public ShortDescriptionAttribute(string shortDescription)
			: base(shortDescription) { }
	}
}
