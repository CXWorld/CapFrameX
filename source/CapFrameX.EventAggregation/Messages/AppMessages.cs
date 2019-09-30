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
	}
}
