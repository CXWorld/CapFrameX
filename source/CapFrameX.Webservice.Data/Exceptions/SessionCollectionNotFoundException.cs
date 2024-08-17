using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.Exceptions
{
	public class SessionCollectionNotFoundException: Exception
	{
		public SessionCollectionNotFoundException(string msg) : base(msg) { }
	}
}
