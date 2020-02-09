using CapFrameX.Webservice.Data;
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Interfaces;
using CapFrameX.Webservice.Data.Queries;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Implementation.Handlers
{
	public class GetCaptureCollectionByIdHandler : IRequestHandler<GetCaptureCollectionByIdQuery, CaptureCollection>
	{
		private readonly IValidator<GetCaptureCollectionByIdQuery> _validator;
		private readonly ICapturesService _capturesService;

		public GetCaptureCollectionByIdHandler(IValidator<GetCaptureCollectionByIdQuery> validator, ICapturesService capturesService)
		{
			_validator = validator;
			_capturesService = capturesService;
		}
		public async Task<CaptureCollection> Handle(GetCaptureCollectionByIdQuery request, CancellationToken cancellationToken)
		{
			_validator.ValidateAndThrow(request);
			return await _capturesService.GetCaptureCollectionById(request.Id);
		}
	}
}
