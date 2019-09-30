using CapFrameX.Contracts.PresentMonInterface;

namespace CapFrameX.PresentMonInterface
{
	public class PresentMonStartInfo : IServiceStartInfo
	{
		public string FileName { get; set; }

		public string Arguments { get; set; }

		public bool CreateNoWindow { get; set; }

		public bool RunWithAdminRights { get; set; }

		public bool RedirectStandardOutput { get; set; }

		public bool UseShellExecute { get; set; }
	}
}
