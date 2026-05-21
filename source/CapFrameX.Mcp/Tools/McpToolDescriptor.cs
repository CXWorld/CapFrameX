using Newtonsoft.Json.Linq;
using System.Reflection;

namespace CapFrameX.Mcp.Tools
{
    internal class McpToolDescriptor
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public JObject InputSchema { get; set; }
        public MethodInfo Method { get; set; }
        public ParameterInfo[] Parameters { get; set; }
    }
}
