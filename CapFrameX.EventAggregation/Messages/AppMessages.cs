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
	}
}
