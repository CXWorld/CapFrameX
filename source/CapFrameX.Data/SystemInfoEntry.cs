using CapFrameX.Contracts.Data;
using System.Linq;

namespace CapFrameX.Data
{
	public class SystemInfoEntry : ISystemInfoEntry
	{
		public string Letter => Key.First().ToString();
		public string Key { get; set; }
		public string Value { get; set; }
		public string IsSelected { get; set; }
	}
}
