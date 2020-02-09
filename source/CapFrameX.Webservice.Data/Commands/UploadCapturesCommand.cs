using CapFrameX.Webservice.Data.DTO;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.Commands
{
	public class UploadCapturesCommand: IRequest<CaptureCollectionMetadata>
	{
		public string AppVersion { get; set; }
		public List<Capture> CaptureFiles { get; set; }
	}

	public class UploadCaptureCommandValidator : AbstractValidator<UploadCapturesCommand>
	{
		public UploadCaptureCommandValidator()
		{
			RuleFor(x => x.AppVersion).NotEmpty().WithMessage("AppVersion field is required");
			RuleFor(x => x.CaptureFiles).NotEmpty().WithMessage("Capture Blobs is required");
		}
	}
}
