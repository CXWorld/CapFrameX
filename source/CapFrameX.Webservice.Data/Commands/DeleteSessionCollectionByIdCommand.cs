using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.Commands
{
	public class DeleteSessionCollectionByIdCommand: IRequest
	{
		public Guid Id { get; set; }
		public Guid? UserId { get; set; }

	}

	public class DeleteSessionCollectionByIdCommandValidator : AbstractValidator<DeleteSessionCollectionByIdCommand>
	{
		public DeleteSessionCollectionByIdCommandValidator()
		{
			RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required");
			RuleFor(x => x.UserId).NotEmpty().WithMessage("You must be logged in to delete a session");
		}
	}
}
