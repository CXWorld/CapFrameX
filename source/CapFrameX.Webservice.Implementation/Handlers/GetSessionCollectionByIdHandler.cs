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
	public class GetSessionCollectionByIdHandler : IRequestHandler<GetSessionCollectionByIdQuery, SessionCollectionDTO>
	{
		private readonly IValidator<GetSessionCollectionByIdQuery> _validator;
		private readonly ISessionService _capturesService;

		public GetSessionCollectionByIdHandler(IValidator<GetSessionCollectionByIdQuery> validator, ISessionService capturesService)
		{
			_validator = validator;
			_capturesService = capturesService;
		}
		public async Task<SessionCollectionDTO> Handle(GetSessionCollectionByIdQuery request, CancellationToken cancellationToken)
		{
			_validator.ValidateAndThrow(request);
			return new SessionCollectionDTO();
		}
	}
}
