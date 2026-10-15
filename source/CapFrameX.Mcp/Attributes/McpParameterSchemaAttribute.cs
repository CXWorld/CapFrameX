using System;

namespace CapFrameX.Mcp.Attributes
{
    /// <summary>
    /// Replaces the generated JSON Schema of a tool parameter, for free-form JSON parameters whose
    /// structure the CLR type cannot express. A <c>Description</c> attribute is still applied.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    public sealed class McpParameterSchemaAttribute : Attribute
    {
        public McpParameterSchemaAttribute(string schemaJson)
        {
            SchemaJson = schemaJson;
        }

        public string SchemaJson { get; }
    }
}
