using System;

namespace CapFrameX.Mcp.Attributes
{
    /// <summary>
    /// Marks a method as an MCP tool. The method is exposed to MCP clients with
    /// the given (or auto-generated) name and description.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class McpServerToolAttribute : Attribute
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }
}
