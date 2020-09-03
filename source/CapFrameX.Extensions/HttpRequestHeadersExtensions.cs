using System.Net.Http.Headers;

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
