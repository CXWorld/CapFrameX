using CapFrameX.Webservice.Data.DTO;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.Queries
{
	public class GetCaptureCollectionByIdQuery: IRequest<CaptureCollection>
	{
		public Guid Id { get; set; }
	}

    public class GetCaptureByIdQueryValidator : AbstractValidator<GetCaptureCollectionByIdQuery>
    {
        public GetCaptureByIdQueryValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
        }
    }
}
