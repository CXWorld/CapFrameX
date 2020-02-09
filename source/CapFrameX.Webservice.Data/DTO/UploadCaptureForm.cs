using FluentValidation;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.DTO
{
	public class UploadCapturesForm
	{
		public string AppVersion { get; set; }
		public List<IFormFile> Capture { get; set; } = new List<IFormFile>();
	}

	public class UploadCapturesFormValidator: AbstractValidator<UploadCapturesForm>
	{
		public UploadCapturesFormValidator()
		{
			RuleFor(x => x.AppVersion).NotEmpty().WithMessage("appVersion is required");
			RuleFor(x => x.Capture).NotEmpty().WithMessage("At least one capture file is required");
		}
	}
}
