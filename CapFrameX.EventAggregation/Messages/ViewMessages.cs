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

		public class ResetRecord { }

		public class SelectSession : UpdateSession
		{
			public SelectSession(Session ocatSession, OcatRecordInfo recordInfo) :
				base(ocatSession, recordInfo)
			{

			}
		}

		public class ShowOverlay { }

		public class HideOverlay { }
	}
}
