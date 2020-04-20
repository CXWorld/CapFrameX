using CapFrameX.Sensor.Reporting.Contracts;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.Queries
{
	public class GetSensorReportsForSessionCollectionFileHashQuery: IRequest<IEnumerable<ISensorReportItem>>
	{
		public Guid FileId { get; set; }
	}
}
