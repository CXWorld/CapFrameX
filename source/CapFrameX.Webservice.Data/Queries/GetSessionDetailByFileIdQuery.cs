using CapFrameX.Sensor.Reporting.Contracts;
using CapFrameX.Webservice.Data.DTO;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.Queries
{
	public class GetSessionDetailByFileIdQuery: IRequest<SessionDetailDTO>
	{
		public Guid FileId { get; set; }
	}
}
