using CapFrameX.Webservice.Data.DTO;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.Queries
{
	public class GetSessionCollectionByIdQuery: IRequest<SessionCollectionDTO>
	{
		public Guid Id { get; set; }
	}

    public class GetCaptureByIdQueryValidator : AbstractValidator<GetSessionCollectionByIdQuery>
    {
        public GetCaptureByIdQueryValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
        }
    }
}
