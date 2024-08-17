using AutoMapper;
using CapFrameX.Webservice.Data.Commands;
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Interfaces;
using FluentValidation;
using MediatR;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CapFrameX.Webservice.Data.Extensions;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;

namespace CapFrameX.Webservice.Implementation.Handlers
{
	public class UploadCapturesHandler : IRequestHandler<UploadSessionsCommand, Guid>
	{
		private readonly IValidator<UploadSessionsCommand> _validator;
		private readonly ISessionService _capturesService;
		private readonly IMapper _mapper;
		private static IFrametimeStatisticProviderOptions _frametimeStatisticProviderOptions = new FrametimeStatisticProviderOptions()
		{
			MovingAverageWindowSize = 500,
			IntervalAverageWindowTime = 500,
			FpsValuesRoundingDigits = 2
		};
		private static FrametimeStatisticProvider _frametimeStatisticProvider =  new FrametimeStatisticProvider(_frametimeStatisticProviderOptions);

		public UploadCapturesHandler(IValidator<UploadSessionsCommand> validator, ISessionService capturesService, IMapper mapper)
		{
			_validator = validator;
			_capturesService = capturesService;
			_mapper = mapper;
        }

		public async Task<Guid> Handle(UploadSessionsCommand command, CancellationToken cancellationToken)
		{
			_validator.ValidateAndThrow(command);

			var sessions = new List<SqSessionData>();
			foreach(var session in command.Sessions)
			{
				var sessionData = _mapper.Map<SqSessionData>(session);
				var fileBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(session));

				var assetId = await _capturesService.UploadAsset(fileBytes.Compress(), session.Hash + ".json.gz");
				sessionData.File = new string[] { assetId.ToString() };

				var frametimes = session.GetFrametimeTimeWindow(0, 1000, _frametimeStatisticProviderOptions);

				sessionData.AverageFps = _frametimeStatisticProvider.GetFpsMetricValue(frametimes, EMetric.Average);
				sessionData.P1Fps = _frametimeStatisticProvider.GetFpsMetricValue(frametimes, EMetric.P1);

				sessions.Add(sessionData);
			}

			var data = new SqSessionCollectionData() { 
				Sub = command.UserId,
				Description = command.Description,
				Sessions = sessions.ToArray()
			};
			return await _capturesService.SaveSessionCollection(data);
		}
	}
}
