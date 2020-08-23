using CapFrameX.Contracts.Data;
using System;
using System.Linq;
using System.Reflection;

namespace CapFrameX.Data
{
    public class AppVersionProvider : IAppVersionProvider
	{
		private readonly Version _version;
		public AppVersionProvider()
		{
			_version = GetAssemblyByName("CapFrameX").GetName().Version;
		}
		public Version GetAppVersion()
		{
			return _version;
		}

		private static Assembly GetAssemblyByName(string name)
		{
			return AppDomain.CurrentDomain.GetAssemblies().
				   SingleOrDefault(assembly => assembly.GetName().Name == name);
		}
	}
}
