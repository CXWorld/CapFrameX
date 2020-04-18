using System;
using System.Collections.Generic;
using System.Text;
using CapFrameX.Webservice.Data.DTO;
using MediatR;

namespace CapFrameX.Webservice.Data.Commands
{
	public class AddProcessCommand : IRequest
	{
		public ProcessListDataDTO Process { get; set; }
	}
}
