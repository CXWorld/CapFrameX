namespace CapFrameX.Mcp.Protocol
{
    internal static class McpProtocol
    {
        public const string Version = "2024-11-05";
        public const string ServerName = "CapFrameX";
        public const string EndpointPath = "/mcp";
        public const string Instructions =
            "Tools of the running CapFrameX instance: recorded captures and their statistics, live capture control, " +
            "sensors, and the overlay (OSD). Before creating or changing a tile overlay profile (a design of the tile " +
            "editor), call cfx_get_tile_design_guide, and check every change with cfx_render_tile_profile.";

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
