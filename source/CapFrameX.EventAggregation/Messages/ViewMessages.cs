using CapFrameX.Contracts.Data;
using CapFrameX.Data;

namespace CapFrameX.EventAggregation.Messages
{
	public abstract class ViewMessages
	{
		public class UpdateSession
		{
			public ISession CurrentSession { get; }
			public IFileRecordInfo RecordInfo { get; }

			public UpdateSession(ISession session, IFileRecordInfo recordInfo)
			{
				CurrentSession = session;
				RecordInfo = recordInfo;
			}
		}

		public class ResetRecord { }

		public class SelectSession : UpdateSession
		{
			public SelectSession(ISession session, IFileRecordInfo recordInfo) :
				base(session, recordInfo)
			{
			}
		}

		public class SetFileRecordInfoExternal
		{
			public IFileRecordInfo RecordInfo { get; }

			public SetFileRecordInfoExternal(IFileRecordInfo recordInfo)
			{
				RecordInfo = recordInfo;
			}
		}

		public class UpdateRecordInfos
		{
			public IFileRecordInfo RecordInfo { get; }

			public UpdateRecordInfos(IFileRecordInfo recordInfo)
			{
				RecordInfo = recordInfo;
			}
		}

		public class UpdateProcessIgnoreList { }
	}
}
