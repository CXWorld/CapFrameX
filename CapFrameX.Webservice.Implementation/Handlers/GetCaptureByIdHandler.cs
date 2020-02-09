using CapFrameX.Webservice.Data;
using CapFrameX.Webservice.Data.Interfaces;
using CapFrameX.Webservice.Data.Queries;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Implementation.Handlers
{
	public class GetCaptureByIdHandler : IRequestHandler<GetCaptureByIdQuery, Capture>
	{
		private readonly ICapturesService _capturesService;

		public GetCaptureByIdHandler(ICapturesService capturesService)
		{
			_capturesService = capturesService;
		}
		public async Task<Capture> Handle(GetCaptureByIdQuery request, CancellationToken cancellationToken)
		{
			return await _capturesService.GetCaptureById(request.Id);
		}
	}
}
