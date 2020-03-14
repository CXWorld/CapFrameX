using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Extensions
{
	public static class HttpRequestHeadersExtensions
	{
		public static HttpRequestHeaders AddCXClientUserAgent(this HttpRequestHeaders headers)
		{
			headers.UserAgent.Add(new ProductInfoHeaderValue("CX_Client", "0"));
			return headers;
		}
	}
}
