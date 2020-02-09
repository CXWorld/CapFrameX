using FluentValidation;
using System;

namespace CapFrameX.Webservice.Data.DTO
{
	public class Capture
	{
		public Guid Id { get; set; }
		public Guid CollectionId { get; set; }
		public string Name { get; set; }
		public byte[] BlobBytes { get; set; }
	}
}
