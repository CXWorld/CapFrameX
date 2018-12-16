using System.Linq;

namespace CapFrameX.OcatInterface
{
	public class SystemInfo
	{
		public string Letter => Key.First().ToString();
		public string Key { get; set; }
		public string Value { get; set; }
	}
}
