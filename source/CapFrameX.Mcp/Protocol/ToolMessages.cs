using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace CapFrameX.Mcp.Protocol
{
    internal class ToolsListResult
    {
        [JsonProperty("tools")]
        public List<ToolDefinition> Tools { get; set; } = new List<ToolDefinition>();
    }

    internal class ToolDefinition
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("inputSchema")]
        public JObject InputSchema { get; set; }
    }

    internal class ToolsCallParams
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("arguments")]
        public JObject Arguments { get; set; }
    }

    internal class ToolsCallResult
    {
        [JsonProperty("content")]
        public List<ToolContent> Content { get; set; } = new List<ToolContent>();

        [JsonProperty("isError", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsError { get; set; }
    }

    internal class ToolContent
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "text";

        [JsonProperty("text")]
        public string Text { get; set; }

        public static ToolContent FromText(string text) => new ToolContent { Text = text };
    }
}
