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
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Statistics.PlotBuilder;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.PlotBuilder.Contracts;
using System.IO;
using OxyPlot;

namespace CapFrameX.Webservice.Implementation.Handlers
{
	public class GetSessionDetailByFileIdHandler : IRequestHandler<GetSessionDetailByFileIdQuery, SessionDetailDTO>
	{
		private readonly ISessionService _sessionService;

		public GetSessionDetailByFileIdHandler(ISessionService sessionService) {
			_sessionService = sessionService;
		}

		public async Task<SessionDetailDTO> Handle(GetSessionDetailByFileIdQuery request, CancellationToken cancellationToken)
		{
			var (fileName, fileBytes) = await _sessionService.DownloadAsset(request.FileId);
			if (fileName.EndsWith(".gz"))
			{
				fileBytes = fileBytes.Decompress();
			}
			var session = JsonConvert.DeserializeObject<Session>(Encoding.UTF8.GetString(fileBytes));

			var sensorItems = SensorReport.GetReportFromSessionSensorData(session.Runs.Select(r => r.SensorData)).ToArray();

			var frametimeStatisticsProviderOptions = new FrametimeStatisticProviderOptions()
			{
				MovingAverageWindowSize = 1000,
				FpsValuesRoundingDigits = 2
			};
			var plotSettings = new PlotSettings();

			var fpsGraphBuilder = new FpsGraphPlotBuilder(frametimeStatisticsProviderOptions, new FrametimeStatisticProvider(frametimeStatisticsProviderOptions));
			fpsGraphBuilder.BuildPlotmodel(session, plotSettings, 0, 1000, ERemoveOutlierMethod.None);
			var frametimeGraphBuilder = new FrametimePlotBuilder(frametimeStatisticsProviderOptions, new FrametimeStatisticProvider(frametimeStatisticsProviderOptions));
			frametimeGraphBuilder.BuildPlotmodel(session, plotSettings, 0, 1000, ERemoveOutlierMethod.None);

			var exporter = new SvgExporter { Width = 1000, Height = 400 };

			using var frametimeGraphStream = new MemoryStream();
			exporter.Export(frametimeGraphBuilder.PlotModel, frametimeGraphStream);

			using var fpsGraphStream = new MemoryStream();
			exporter.Export(fpsGraphBuilder.PlotModel, fpsGraphStream);

			return new SessionDetailDTO()
			{
				SensorItems = sensorItems,
				FpsGraph = Encoding.UTF8.GetString(fpsGraphStream.ToArray()),
				FrametimeGraph = Encoding.UTF8.GetString(frametimeGraphStream.ToArray())
			};
		}
	}

	class FrametimeStatisticProviderOptions : IFrametimeStatisticProviderOptions
	{
		public int MovingAverageWindowSize { get; set; }
		public int FpsValuesRoundingDigits { get; set; }
	}

	class PlotSettings : IPlotSettings
	{
		public bool ShowGpuLoad { get; set; }

		public bool ShowCpuLoad { get; set; }

		public bool ShowCpuMaxThreadLoad { get; set; }

		public bool IsAnyGraphVisible { get; set; }
	}
}
