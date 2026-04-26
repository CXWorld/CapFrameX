using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Mcp.Protocol
{
    internal class JsonRpcRequest
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; }

        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public JToken Id { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("params", NullValueHandling = NullValueHandling.Ignore)]
        public JToken Params { get; set; }

        public bool IsNotification => Id == null;
    }

    internal class JsonRpcResponse
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonProperty("id")]
        public JToken Id { get; set; }

        [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
        public object Result { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public JsonRpcError Error { get; set; }

        public static JsonRpcResponse OkResult(JToken id, object result) =>
            new JsonRpcResponse { Id = id, Result = result };

        public static JsonRpcResponse ErrorResult(JToken id, int code, string message, object data = null) =>
            new JsonRpcResponse { Id = id, Error = new JsonRpcError { Code = code, Message = message, Data = data } };
    }

    internal class JsonRpcError
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public object Data { get; set; }
    }
}
