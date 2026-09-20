using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Update;
using System;
using System.Linq;
using System.Reflection;

namespace CapFrameX.Data
{
    public class AppVersionProvider : IAppVersionProvider
	{
		private readonly Version _version;
		private readonly EUpdateChannel _releaseChannel;

		public AppVersionProvider()
		{
			var assembly = GetAssemblyByName("CapFrameX");
			_version = assembly.GetName().Version;
			_releaseChannel = ReadReleaseChannel(assembly);
		}

		public Version GetAppVersion()
		{
			return _version;
		}

		public EUpdateChannel GetReleaseChannel()
		{
			return _releaseChannel;
		}

		private static EUpdateChannel ReadReleaseChannel(Assembly assembly)
		{
			var channel = assembly
				.GetCustomAttributes<AssemblyMetadataAttribute>()
				.FirstOrDefault(attribute => attribute.Key == "ReleaseChannel")
				?.Value;

			if (string.Equals(channel, "release", StringComparison.OrdinalIgnoreCase))
				return EUpdateChannel.Release;

			// Missing or malformed metadata must never make an unclassified build look like a release.
			return EUpdateChannel.Beta;
		}

		private static Assembly GetAssemblyByName(string name)
		{
			return AppDomain.CurrentDomain.GetAssemblies().
				   SingleOrDefault(assembly => assembly.GetName().Name == name);
		}
	}
}
