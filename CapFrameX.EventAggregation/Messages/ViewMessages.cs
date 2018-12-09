using CapFrameX.OcatInterface;

namespace CapFrameX.EventAggregation.Messages
{
	public abstract class ViewMessages
	{
		public class UpdateSession
		{
			public Session OcatSession { get; }
			public OcatRecordInfo RecordInfo { get; }

			public UpdateSession(Session ocatSession, OcatRecordInfo recordInfo)
			{
				OcatSession = ocatSession;
				RecordInfo = recordInfo;
			}
		}
	}
}
