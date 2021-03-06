﻿using AutoMapper;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Webservice.Data.DTO;
using System.Linq;

namespace CapFrameX.Webservice.Data.Mappings
{
	public class SessionCollectionProfile : Profile
	{

		public SessionCollectionProfile()
		{
			CreateMap<SqSessionCollection, SessionCollectionDTO>()
				.ForMember(dest => dest.Id, opts => opts.MapFrom(src => src.Id))
				.ForMember(dest => dest.UserId, opts => opts.MapFrom(src => src.Data.Sub))
				.ForMember(dest => dest.Timestamp, opts => opts.MapFrom(src => src.Created.UtcDateTime))
				.ForMember(dest => dest.Description, opts => opts.MapFrom(src => src.Data.Description))
				.ForMember(dest => dest.Sessions, opts => opts.Ignore());

			CreateMap<SqSessionCollection, SessionCollectionReducedDTO>()
				.ForMember(dest => dest.Id, opts => opts.MapFrom(src => src.Id))
				.ForMember(dest => dest.UserId, opts => opts.MapFrom(src => src.Data.Sub))
				.ForMember(dest => dest.Timestamp, opts => opts.MapFrom(src => src.Created.UtcDateTime))
				.ForMember(dest => dest.Description, opts => opts.MapFrom(src => src.Data.Description))
				.ForMember(dest => dest.Sessions, opts => opts.MapFrom(src => src.Data.Sessions));

			CreateMap<SqSessionData, SessionReducedDTO>()
				.ForMember(dest => dest.AppVersion, opts => opts.MapFrom(src => src.AppVersion))
				.ForMember(dest => dest.GameName, opts => opts.MapFrom(src => src.GameName))
				.ForMember(dest => dest.ProcessName, opts => opts.MapFrom(src => src.ProcessName))
				.ForMember(dest => dest.CreationDate, opts => opts.MapFrom(src => src.CreationDate))
				.ForMember(dest => dest.Comment, opts => opts.MapFrom(src => src.Comment))
				.ForMember(dest => dest.FileId, opts => opts.MapFrom(src => src.File.FirstOrDefault()))
				.ForMember(dest => dest.SessionHash, opts => opts.MapFrom(src => src.Hash));

			CreateMap<Session, SqSessionData>()
				.ForMember(dest => dest.AppVersion, opts => opts.MapFrom(src => src.Info.AppVersion))
				.ForMember(dest => dest.Comment, opts => opts.MapFrom(src => src.Info.Comment))
				.ForMember(dest => dest.CreationDate, opts => opts.MapFrom(src => src.Info.CreationDate))
				.ForMember(dest => dest.GameName, opts => opts.MapFrom(src => src.Info.GameName))
				.ForMember(dest => dest.ProcessName, opts => opts.MapFrom(src => src.Info.ProcessName))
				.ForMember(dest => dest.Hash, opts => opts.MapFrom(src => src.Hash))
				.ForMember(dest => dest.Cpu, opts => opts.MapFrom(src => src.Info.Processor))
				.ForMember(dest => dest.Gpu, opts => opts.MapFrom(src => src.Info.GPU))
				.ForMember(dest => dest.Mainboard, opts => opts.MapFrom(src => src.Info.Motherboard))
				.ForMember(dest => dest.Ram, opts => opts.MapFrom(src => src.Info.SystemRam))
				.ForMember(dest => dest.AverageFps, opts => opts.MapFrom(src => 0))
				.ForMember(dest => dest.P1Fps, opts => opts.MapFrom(src => 0));
		}
	}
}
