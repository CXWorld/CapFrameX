using CapFrameX.Webservice.Data.DTO;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.Queries
{
	public class SearchSessionsQuery: IRequest<SqSessionData[]>
	{
		public string Cpu { get; set; }
		public string Gpu { get; set; }
		public string GameName { get; set; }
		public string Mainbaord { get; set; }
		public string Ram { get; set; }
		public string Comment { get; set; }
	}
}
