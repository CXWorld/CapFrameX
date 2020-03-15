using AutoMapper;
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
		private readonly IMapper _mapper;

		public UploadCapturesHandler(IValidator<UploadSessionsCommand> validator, ISessionService capturesService, IMapper mapper)
		{
			_validator = validator;
			_capturesService = capturesService;
			_mapper = mapper;
		}

		public async Task<Guid> Handle(UploadSessionsCommand command, CancellationToken cancellationToken)
		{
			_validator.ValidateAndThrow(command);

			var data = new SqSessionCollectionData() { 
				Sub = command.UserId,
				Description = command.Description,
				Sessions = _mapper.Map<SqSessionData[]>(command.Sessions.Cast<Session>())
			};
			return await _capturesService.SaveSessionCollection(data);
		}
	}
}
