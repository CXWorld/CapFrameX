using CapFrameX.Webservice.Data.Commands;
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Interfaces;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Implementation.Handlers
{
	public class UploadCapturesHandler: IRequestHandler<UploadSessionsCommand, Guid>
	{
		private readonly IValidator<UploadSessionsCommand> _validator;
		private readonly ISessionService _capturesService;

		public UploadCapturesHandler(IValidator<UploadSessionsCommand> validator, ISessionService capturesService) {
			_validator = validator;
			_capturesService = capturesService;
		}

		public async Task<Guid> Handle(UploadSessionsCommand command, CancellationToken cancellationToken)
		{
			_validator.ValidateAndThrow(command);
			return Guid.NewGuid();
		}
	}
}
