using CapFrameX.Webservice.Data.DTO;

namespace CapFrameX.Webservice.Data.Mappings
{
	public static class AppNotificationMappings
	{
		public static SqAppNotificationDataDTO ToAppNotificationDto(this SqAppNotificationData source)
		{
			if (source is null)
			{
				return null;
			}

			return new SqAppNotificationDataDTO
			{
				IsActive = source.IsActive,
				Message = source.Message,
				Timestamp = source.Timestamp
			};
		}
	}
}
