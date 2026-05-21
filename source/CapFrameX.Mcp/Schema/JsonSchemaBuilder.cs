using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace CapFrameX.Mcp.Schema
{
    /// <summary>
    /// Generates JSON Schema for tool parameter lists. Scope is intentionally
    /// limited to what Phase 1 tools need: primitives, nullable types, enums,
    /// arrays / IEnumerable of those, and simple POCO objects.
    /// </summary>
    internal static class JsonSchemaBuilder
    {
        public static JObject BuildSchemaForParameters(IEnumerable<ParameterInfo> parameters)
        {
            var properties = new JObject();
            var required = new JArray();

            foreach (var p in parameters)
            {
                var prop = BuildSchemaForType(p.ParameterType);
                var description = p.GetCustomAttribute<DescriptionAttribute>()?.Description;
                if (!string.IsNullOrEmpty(description))
                    prop["description"] = description;

                properties[p.Name] = prop;

                if (!p.IsOptional && !IsNullable(p.ParameterType))
                    required.Add(p.Name);
            }

            var schema = new JObject
            {
                ["type"] = "object",
                ["properties"] = properties,
            };
            if (required.Count > 0)
                schema["required"] = required;

            return schema;
        }

        private static JObject BuildSchemaForType(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying == typeof(string))
                return new JObject { ["type"] = "string" };
            if (underlying == typeof(bool))
                return new JObject { ["type"] = "boolean" };
            if (underlying == typeof(int) || underlying == typeof(long)
                || underlying == typeof(short) || underlying == typeof(byte))
                return new JObject { ["type"] = "integer" };
            if (underlying == typeof(float) || underlying == typeof(double) || underlying == typeof(decimal))
                return new JObject { ["type"] = "number" };

            if (underlying.IsEnum)
            {
                var values = new JArray();
                foreach (var name in Enum.GetNames(underlying))
                    values.Add(name);
                return new JObject { ["type"] = "string", ["enum"] = values };
            }

            // Arrays / IEnumerable<T>
            var elementType = GetEnumerableElementType(underlying);
            if (elementType != null)
            {
                return new JObject
                {
                    ["type"] = "array",
                    ["items"] = BuildSchemaForType(elementType),
                };
            }

            // Fallback: simple POCO — list public, settable properties.
            if (underlying.IsClass)
            {
                var props = new JObject();
                foreach (var prop in underlying.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (!prop.CanRead) continue;
                    props[prop.Name] = BuildSchemaForType(prop.PropertyType);
                }
                return new JObject { ["type"] = "object", ["properties"] = props };
            }

            return new JObject { ["type"] = "object" };
        }

        private static bool IsNullable(Type type)
        {
            if (!type.IsValueType) return true;
            return Nullable.GetUnderlyingType(type) != null;
        }

        private static Type GetEnumerableElementType(Type type)
        {
            if (type == typeof(string)) return null;
            if (type.IsArray) return type.GetElementType();

            foreach (var iface in type.GetInterfaces().Concat(new[] { type }))
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return iface.GetGenericArguments()[0];
            }
            return null;
        }
    }
}
