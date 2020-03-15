using CapFrameX.Data.Session.Classes;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.Entities
{
	public class SessionProxy
	{
		public Guid SessionCollectionId { get; set; }
		public Guid Id { get; set; }
		public Session Session { get; set; }

		public virtual SessionCollection SessionCollection { get; set; }
	}
}
