using CapFrameX.Contracts.Data;
using CapFrameX.OcatInterface;

namespace CapFrameX.EventAggregation.Messages
{
	public abstract class ViewMessages
	{
		public class UpdateSession
		{
			public Session OcatSession { get; }
			public IFileRecordInfo RecordInfo { get; }

			public UpdateSession(Session ocatSession, IFileRecordInfo recordInfo)
			{
				OcatSession = ocatSession;
				RecordInfo = recordInfo;
			}
		}

		public class ResetRecord { }

		public class SelectSession : UpdateSession
		{
			public SelectSession(Session ocatSession, IFileRecordInfo recordInfo) :
				base(ocatSession, recordInfo)
			{

			}
		}

		public class ShowOverlay { }

		public class HideOverlay { }

        public class UpdateProcessIgnoreList { }
    }
}
