using CapFrameX.Data.Session.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.DTO
{
	public class SessionCollectionReducedDTO
	{
		public Guid Id { get; set; }
		public Guid? UserId { get; set; }
		public DateTime Timestamp { get; set; }
		public string Description { get; set; }
		public virtual IEnumerable<SessionReducedDTO> Sessions { get; set; }
	}

	public class SessionReducedDTO
	{
		public string AppVersion { get; set; }
		public string Comment { get; set; }
		public string ProcessName { get; set; }
		public string GameName { get; set; }
		public DateTime CreationDate { get; set; }
	}
}
