using System;

namespace CapFrameX.Mcp.Attributes
{
    /// <summary>
    /// Marks a class as a container of MCP tools. All public methods on the class
    /// annotated with <see cref="McpServerToolAttribute"/> are registered as tools
    /// callable via the MCP protocol.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class McpServerToolTypeAttribute : Attribute
    {
    }
}
