using CapFrameX.Data.Session.Classes;
using CapFrameX.Webservice.Data.Commands;
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Interfaces;
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
	public class UploadCapturesHandler : IRequestHandler<UploadSessionsCommand, Guid>
	{
		private readonly IValidator<UploadSessionsCommand> _validator;
		private readonly ISessionService _capturesService;

		public UploadCapturesHandler(IValidator<UploadSessionsCommand> validator, ISessionService capturesService)
		{
			_validator = validator;
			_capturesService = capturesService;
		}

		public async Task<Guid> Handle(UploadSessionsCommand command, CancellationToken cancellationToken)
		{
			_validator.ValidateAndThrow(command);
			return await _capturesService.SaveSessionCollection(new Data.Entities.SessionCollection()
			{
				Sessions = command.Sessions.Select(s => new Data.Entities.SessionProxy()
				{
					Session = (Session)s
				}).ToArray(),
				UserId = command.UserId,
				Timestamp = DateTime.UtcNow,
				Name = "",
				Description = ""
			});
		}
	}
}
