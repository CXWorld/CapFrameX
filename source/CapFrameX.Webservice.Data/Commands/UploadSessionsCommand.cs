using CapFrameX.Data.Session.Contracts;
using CapFrameX.Webservice.Data.DTO;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.Commands
{
	public class UploadSessionsCommand: IRequest<Guid>
	{
		public Guid? UserId { get; set; }
		public IEnumerable<ISession> Sessions { get; set; }
	}

	public class UploadSessionsCommandValidator : AbstractValidator<UploadSessionsCommand>
	{
		public UploadSessionsCommandValidator()
		{
			RuleFor(x => x.Sessions).NotEmpty().WithMessage("Sesisons are required");
		}
	}
}
