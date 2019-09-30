namespace CapFrameX.Contracts.PresentMonInterface
{
	public interface IServiceStartInfo
	{
		string FileName { get; }

		string Arguments { get; }

		bool CreateNoWindow { get; }

		bool RunWithAdminRights { get; }

		bool RedirectStandardOutput { get; }

		bool UseShellExecute { get; }
	}
}
