using CapFrameX.Data.Session.Contracts;
using CapFrameX.Webservice.Data.DTO;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.Commands
{
	public class UploadSessionsCommand : IRequest<Guid>
	{
		public Guid? UserId { get; set; }
		public IEnumerable<ISession> Sessions { get; set; }
		public string Description { get; set; }
	}

	public class UploadSessionsCommandValidator : AbstractValidator<UploadSessionsCommand>
	{
		public UploadSessionsCommandValidator()
		{
			RuleFor(x => x.Sessions).NotEmpty().WithMessage("Sesisons are required");
			RuleForEach(x => x.Sessions).SetValidator(new SessionValidator());
		}
	}

	public class SessionValidator: AbstractValidator<ISession> {
		public SessionValidator()
		{
			RuleFor(x => x.Hash).NotEmpty().WithMessage("Hash os Session is required");
			RuleFor(x => x.Runs).NotEmpty().WithMessage("Session must have at least one Run");
			RuleFor(x => x.Info).NotEmpty().WithMessage("SessionInfo is required");
			RuleForEach(x => x.Runs).SetValidator(new SessionRunValidator());
		}
	}

	public class SessionRunValidator: AbstractValidator<ISessionRun>
	{
		public SessionRunValidator()
		{
			RuleFor(x => x.Hash).NotEmpty().WithMessage("Hash of SessionRun is required");
			RuleFor(x => x.CaptureData).NotEmpty().WithMessage("CaptureData is required");
			RuleFor(x => x.CaptureData).SetValidator(new CaptureDataValidator());
		}
	}

	public class CaptureDataValidator: AbstractValidator<ISessionCaptureData>
	{
		public CaptureDataValidator()
		{
			RuleFor(x => x.TimeInSeconds).NotEmpty().WithMessage("Need at least one Measurepoint");
		}
	}
}
