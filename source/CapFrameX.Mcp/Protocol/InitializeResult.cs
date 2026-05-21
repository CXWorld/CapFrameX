using Newtonsoft.Json;

namespace CapFrameX.Mcp.Protocol
{
    internal class InitializeResult
    {
        [JsonProperty("protocolVersion")]
        public string ProtocolVersion { get; set; }

        [JsonProperty("capabilities")]
        public ServerCapabilities Capabilities { get; set; }

        [JsonProperty("serverInfo")]
        public ServerInfo ServerInfo { get; set; }
    }

    internal class ServerCapabilities
    {
        [JsonProperty("tools")]
        public ToolsCapability Tools { get; set; } = new ToolsCapability();
    }

    internal class ToolsCapability
    {
        [JsonProperty("listChanged")]
        public bool ListChanged { get; set; } = false;
    }

    internal class ServerInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }
    }
}
