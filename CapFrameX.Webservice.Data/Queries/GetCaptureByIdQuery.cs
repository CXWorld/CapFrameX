using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.Queries
{
	public class GetCaptureByIdQuery: IRequest<Capture>
	{
		public Guid Id { get; set; }
	}

    public class GetCaptureByIdQueryValidator : AbstractValidator<GetCaptureByIdQuery>
    {
        public GetCaptureByIdQueryValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
        }
    }
}
