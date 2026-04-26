using CapFrameX.Mcp.Attributes;
using System.ComponentModel;

namespace CapFrameX.Mcp.Tools
{
    [McpServerToolType]
    public static class PingTools
    {
        [McpServerTool(Name = "cfx_ping", Description = "Returns 'pong' if the CapFrameX MCP server is reachable. Useful as a connectivity health check.")]
        public static string CfxPing(
            [Description("Optional message to echo back")] string echo = null)
        {
            return string.IsNullOrEmpty(echo) ? "pong from CapFrameX MCP" : "pong: " + echo;
        }
    }
}
