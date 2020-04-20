using CapFrameX.Sensor.Reporting.Contracts;
using CapFrameX.Webservice.Data.Exceptions;
using CapFrameX.Webservice.Data.Interfaces;
using CapFrameX.Webservice.Data.Queries;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CapFrameX.Webservice.Data.Extensions;
using Newtonsoft.Json;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Sensor.Reporting;

namespace CapFrameX.Webservice.Implementation.Handlers
{
	public class GetSensorReportsForSessionCollectionFileHashHandler : IRequestHandler<GetSensorReportsForSessionCollectionFileHashQuery, IEnumerable<ISensorReportItem>>
	{
		private readonly ISessionService _sessionService;

		public GetSensorReportsForSessionCollectionFileHashHandler(ISessionService sessionService) {
			_sessionService = sessionService;
		}

		public async Task<IEnumerable<ISensorReportItem>> Handle(GetSensorReportsForSessionCollectionFileHashQuery request, CancellationToken cancellationToken)
		{
			var (fileName, fileBytes) = await _sessionService.DownloadAsset(request.FileId);
			if (fileName.EndsWith(".gz"))
			{
				fileBytes = fileBytes.Decompress();
			}
			var session = JsonConvert.DeserializeObject<Session>(Encoding.UTF8.GetString(fileBytes));

			return SensorReport.GetReportFromSessionSensorData(session.Runs.Select(r => r.SensorData));
		}
	}
}
