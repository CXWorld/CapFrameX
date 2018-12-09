using CapFrameX.OcatInterface;

namespace CapFrameX.EventAggregation.Messages
{
	public abstract class ViewMessages
	{
		public class UpdateSession
		{
			public Session OcatSession { get; }

			public UpdateSession(Session ocatSession)
			{
				OcatSession = ocatSession;
			}
		}
	}
}
