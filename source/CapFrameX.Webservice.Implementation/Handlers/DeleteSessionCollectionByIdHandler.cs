using CapFrameX.Webservice.Data.Commands;
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
	public class DeleteSessionCollectionByIdHandler : AsyncRequestHandler<DeleteSessionCollectionByIdCommand>
	{
		private readonly ISessionService _sessionService;
		private readonly IValidator<DeleteSessionCollectionByIdCommand> _validator;

		public DeleteSessionCollectionByIdHandler(ISessionService sessionService, IValidator<DeleteSessionCollectionByIdCommand> validator)
		{
			_sessionService = sessionService;
			_validator = validator;
		}

		protected override async Task Handle(DeleteSessionCollectionByIdCommand command, CancellationToken cancellationToken)
		{
			_validator.ValidateAndThrow(command);
			await _sessionService.DeleteCollection(command.Id, (Guid)command.UserId);
		}
	}
}
