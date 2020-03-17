using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using CapFrameX.Webservice.Data.Commands;
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CapFrameX.Webservice.Implementation.Handlers
{
	public class AddProcessHandler : AsyncRequestHandler<AddProcessCommand>
	{
		private readonly IProcessListService _processListService;
		private readonly IMapper _mapper;
		private readonly ILogger<AddProcessHandler> _logger;

		public AddProcessHandler(IProcessListService processListService, IMapper mapper, ILogger<AddProcessHandler> logger) {
			_processListService = processListService;
			_mapper = mapper;
			_logger = logger;
		}

		protected override async Task Handle(AddProcessCommand request, CancellationToken cancellationToken)
		{
			var process = _mapper.Map<ProcessListData>(request.Process);
			try
			{
				await _processListService.AddProcess(process);
			} catch (Exception e)
			{
				_logger.LogError(e, "Add Process failed for {@process}", process);
			}
		}
	}
}
