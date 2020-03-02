using AutoMapper;
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CapFrameX.Webservice.Data.Mappings
{
	public class SessionCollectionProfile: Profile
	{
		public SessionCollectionProfile()
		{
			CreateMap<SessionCollection, SessionCollectionDTO>()
				.ForMember(dest => dest.Sessions, opts => opts.MapFrom(src => src.Sessions.Select(s => s.Session)));
		}
	}
}
