using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CapFrameX.Webservice.Data.Extensions
{
	public static class HttpRequestExtensions
	{

		public static bool HasCXClientHeader(this HttpRequest request)
		{
			var userAgentHeaderPresent = request.Headers.TryGetValue("User-Agent", out var uaHeader);
			return userAgentHeaderPresent && uaHeader.All(agent => agent.Contains("CX_Client"));
		}
	}
}
