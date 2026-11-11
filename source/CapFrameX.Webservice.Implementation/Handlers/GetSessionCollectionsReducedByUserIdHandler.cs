using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Mappings;
using CapFrameX.Webservice.Data.Interfaces;
using CapFrameX.Webservice.Data.Queries;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Implementation.Handlers
{
	public class GetSessionCollectionsReducedByUserIdHandler : IRequestHandler<GetSessionCollectionsReducedForUserByIdQuery, IEnumerable<SessionCollectionReducedDTO>>
	{
		private readonly ISessionService _sessionService;
		private readonly IValidator<GetSessionCollectionsReducedForUserByIdQuery> _validator;

		public GetSessionCollectionsReducedByUserIdHandler(ISessionService sessionService, IValidator<GetSessionCollectionsReducedForUserByIdQuery> validator)
		{
			_sessionService = sessionService;
			_validator = validator;
		}
		public async Task<IEnumerable<SessionCollectionReducedDTO>> Handle(GetSessionCollectionsReducedForUserByIdQuery request, CancellationToken cancellationToken)
		{
			_validator.ValidateAndThrow(request);
			return (await _sessionService.GetSessionCollectionsForUser(request.UserId)).Select(SessionCollectionMappings.ToSessionCollectionReducedDto);
		}
	}
}
