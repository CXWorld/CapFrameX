using CapFrameX.Contracts.Data;

namespace CapFrameX.Data
{
	public class CloudManager : ICloudManager
	{
		private readonly LoginManager _loginManager;

		public CloudManager(LoginManager loginManager) {
			_loginManager = loginManager;
		}
	}
}