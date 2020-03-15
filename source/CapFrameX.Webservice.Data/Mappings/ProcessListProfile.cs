using AutoMapper;
using CapFrameX.Webservice.Data.DTO;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.Mappings
{
	public class ProcessListProfile: Profile
	{
		public ProcessListProfile()
		{
			CreateMap<ProcessListData, ProcessListDataDTO>();
		}
	}
}
