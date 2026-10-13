using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CapFrameX.Webservice.Data.Commands;
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Mappings;
using CapFrameX.Webservice.Data.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CapFrameX.Webservice.Implementation.Handlers
{
	public class AddProcessHandler : AsyncRequestHandler<AddProcessCommand>
	{
		private readonly IProcessListService _processListService;
		private readonly ILogger<AddProcessHandler> _logger;

		public AddProcessHandler(IProcessListService processListService, ILogger<AddProcessHandler> logger) {
			_processListService = processListService;
			_logger = logger;
		}

		protected override async Task Handle(AddProcessCommand request, CancellationToken cancellationToken)
		{
			var process = request.Process.ToProcessListData();
			try
			{
				await _processListService.AddProcess(process);
				_logger.LogInformation("Successfully added {processName} to processList", process.Name);
			} catch (Exception e)
			{
				_logger.LogError(e, "Add Process failed for {@process}", process);
			}
		}
	}
}
