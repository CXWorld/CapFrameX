namespace CapFrameX.Mcp.Protocol
{
    internal static class McpProtocol
    {
        public const string Version = "2024-11-05";
        public const string ServerName = "CapFrameX";
        public const string EndpointPath = "/mcp";

        public static class Methods
        {
            public const string Initialize = "initialize";
            public const string Initialized = "notifications/initialized";
            public const string ToolsList = "tools/list";
            public const string ToolsCall = "tools/call";
            public const string Ping = "ping";
        }

        public static class ErrorCodes
        {
            public const int ParseError = -32700;
            public const int InvalidRequest = -32600;
            public const int MethodNotFound = -32601;
            public const int InvalidParams = -32602;
            public const int InternalError = -32603;
        }
    }
}
