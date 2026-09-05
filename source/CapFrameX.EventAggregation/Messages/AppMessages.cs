namespace CapFrameX.EventAggregation.Messages
{
	public abstract class AppMessages
	{
		public class DirectoryObserverState
		{
			public bool IsObserving { get;}

			public DirectoryObserverState(bool isObserving)
			{
				IsObserving = isObserving;
			}
		}

		public class UpdateObservedDirectory
		{
			public string Directory { get; }

			public UpdateObservedDirectory(string directory)
			{
				Directory = directory;
			}	
		}

		public class OpenLoginWindow { }

		public class LoginState
		{
			public bool IsLoggedIn { get; }

			public LoginState(bool loggedIn)
			{
				IsLoggedIn = loggedIn;
			}
		}

		public class CloudFolderChanged	{ }

		public class SelectCloudFolder { }

		/// <summary>
		/// Published by the shell whenever its content becomes visible or invisible
		/// (minimized or hidden to the tray). Deliberately based on visibility, not
		/// focus: CapFrameX may run on a second monitor while a game holds the focus.
		/// </summary>
		public class ShellVisibilityChanged
		{
			public bool IsContentVisible { get; }

			public ShellVisibilityChanged(bool isContentVisible)
			{
				IsContentVisible = isContentVisible;
			}
		}
	}
}
