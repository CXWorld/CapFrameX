using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.Entities
{
	public class SessionCollection
	{
		public Guid Id { get; set; }
		public Guid? UserId { get; set; }
		public DateTime Timestamp { get; set; }
		public string Description { get; set; }
		public virtual ICollection<SessionProxy> Sessions { get; set; }
	}
}
