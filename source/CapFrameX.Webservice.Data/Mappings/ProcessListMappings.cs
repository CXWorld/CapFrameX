using CapFrameX.Webservice.Data.DTO;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Webservice.Data.Mappings
{
	public static class ProcessListMappings
	{
		public static ProcessListDataDTO ToProcessListDataDto(this ProcessListData source)
		{
			if (source is null)
			{
				return null;
			}

			return new ProcessListDataDTO
			{
				Name = source.Name,
				DisplayName = source.DisplayName,
				IsBlacklisted = source.IsBlacklisted
			};
		}

		public static ProcessListData ToProcessListData(this ProcessListDataDTO source)
		{
			if (source is null)
			{
				return null;
			}

			return new ProcessListData
			{
				Name = source.Name,
				DisplayName = source.DisplayName,
				IsBlacklisted = source.IsBlacklisted
			};
		}

		/// <summary>
		/// A missing list maps to an empty array, a missing entry stays null.
		/// </summary>
		public static ProcessListDataDTO[] ToProcessListDataDtos(this IEnumerable<ProcessListData> source)
			=> source?.Select(ToProcessListDataDto).ToArray() ?? new ProcessListDataDTO[0];
	}
}
