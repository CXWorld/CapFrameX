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
	public class UploadCapturesHandler: IRequestHandler<UploadCapturesCommand, CaptureCollectionMetadata>
	{
		private readonly IValidator<UploadCapturesCommand> _validator;
		private readonly ICapturesService _capturesService;

		public UploadCapturesHandler(IValidator<UploadCapturesCommand> validator, ICapturesService capturesService) {
			_validator = validator;
			_capturesService = capturesService;
		}

		public async Task<CaptureCollectionMetadata> Handle(UploadCapturesCommand command, CancellationToken cancellationToken)
		{
			_validator.ValidateAndThrow(command);
			return await _capturesService.SaveCaptures(command.AppVersion, command.CaptureFiles);
		}
	}
}
