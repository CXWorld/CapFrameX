using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.Exceptions
{
	public class CaptureNotFoundException: Exception
	{
		public CaptureNotFoundException(string msg) : base(msg) { }
	}
}
