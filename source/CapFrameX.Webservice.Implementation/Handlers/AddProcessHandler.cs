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

namespace CapFrameX.Webservice.Implementation.Handlers
{
	public class AddProcessHandler : AsyncRequestHandler<AddProcessCommand>
	{
		private readonly IProcessListService _processListService;
		private readonly IMapper _mapper;

		public AddProcessHandler(IProcessListService processListService, IMapper mapper) {
			_processListService = processListService;
			_mapper = mapper;
		}
		protected override async Task Handle(AddProcessCommand request, CancellationToken cancellationToken)
		{
			var process = _mapper.Map<ProcessListData>(request.Process);
			try
			{
				await _processListService.AddProcess(process);
			} catch { }
		}
	}
}
