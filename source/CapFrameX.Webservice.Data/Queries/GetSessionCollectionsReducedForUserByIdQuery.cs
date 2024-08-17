using CapFrameX.Webservice.Data.DTO;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.Queries
{
	public class GetSessionCollectionsReducedForUserByIdQuery: IRequest<IEnumerable<SessionCollectionReducedDTO>>
	{
		public Guid UserId { get; set; }
	}

    public class GetSessionCollectionsReducedForUserByIdQueryValidator : AbstractValidator<GetSessionCollectionsReducedForUserByIdQuery>
    {
        public GetSessionCollectionsReducedForUserByIdQueryValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
        }
    }
}
