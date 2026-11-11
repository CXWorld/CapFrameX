using CapFrameX.Data.Session.Contracts;
using CapFrameX.Webservice.Data.DTO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Webservice.Data.Mappings
{
	public static class SessionCollectionMappings
	{
		/// <summary>
		/// Maps the collection header. <see cref="SessionCollectionDTO.Sessions"/> stays null,
		/// the caller fills it with the sessions downloaded from the assets.
		/// </summary>
		public static SessionCollectionDTO ToSessionCollectionDto(this SqSessionCollection source)
		{
			if (source is null)
			{
				return null;
			}

			return new SessionCollectionDTO
			{
				Id = source.Id,
				UserId = source.Data?.Sub,
				Timestamp = source.Created.UtcDateTime,
				Description = source.Data?.Description
			};
		}

		/// <summary>
		/// Missing sessions map to an empty list, a missing entry stays null.
		/// </summary>
		public static SessionCollectionReducedDTO ToSessionCollectionReducedDto(this SqSessionCollection source)
		{
			if (source is null)
			{
				return null;
			}

			return new SessionCollectionReducedDTO
			{
				Id = source.Id,
				UserId = source.Data?.Sub,
				Timestamp = source.Created.UtcDateTime,
				Description = source.Data?.Description,
				Sessions = source.Data?.Sessions?.Select(ToSessionReducedDto).ToList() ?? new List<SessionReducedDTO>()
			};
		}

		/// <summary>
		/// The first file is the session's asset id. Without a file it is <see cref="Guid.Empty"/>;
		/// a file that is not a GUID throws a <see cref="FormatException"/>.
		/// </summary>
		public static SessionReducedDTO ToSessionReducedDto(this SqSessionData source)
		{
			if (source is null)
			{
				return null;
			}

			var fileId = source.File?.FirstOrDefault();
			return new SessionReducedDTO
			{
				SessionHash = source.Hash,
				AppVersion = source.AppVersion,
				Comment = source.Comment,
				ProcessName = source.ProcessName,
				GameName = source.GameName,
				CreationDate = source.CreationDate,
				FileId = fileId is null ? Guid.Empty : Guid.Parse(fileId)
			};
		}

		/// <summary>
		/// Maps the searchable metadata of an uploaded session. <see cref="SqSessionData.File"/>
		/// and the FPS metrics are left to the caller, which uploads the asset and evaluates the runs.
		/// </summary>
		public static SqSessionData ToSqSessionData(this ISession source)
		{
			if (source is null)
			{
				return null;
			}

			var info = source.Info;
			return new SqSessionData
			{
				Hash = source.Hash,
				AppVersion = info?.AppVersion?.ToString(),
				Comment = info?.Comment,
				CreationDate = info?.CreationDate ?? default,
				GameName = info?.GameName,
				ProcessName = info?.ProcessName,
				Cpu = info?.Processor,
				Gpu = info?.GPU,
				Mainboard = info?.Motherboard,
				Ram = info?.SystemRam
			};
		}
	}
}
