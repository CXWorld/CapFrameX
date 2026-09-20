using CapFrameX.Mcp.Attributes;
using CapFrameX.Mcp.Schema;
using DryIoc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Mcp.Tools
{
    internal class McpToolRegistry
    {
        private readonly IContainer _container;
        private readonly Dictionary<string, McpToolDescriptor> _tools =
            new Dictionary<string, McpToolDescriptor>(StringComparer.Ordinal);

        public McpToolRegistry(IContainer container, IEnumerable<Assembly> assembliesToScan)
        {
            _container = container;
            foreach (var asm in assembliesToScan ?? Enumerable.Empty<Assembly>())
                ScanAssembly(asm);
        }

        public IReadOnlyCollection<McpToolDescriptor> GetAll() => _tools.Values;

        public bool TryGet(string name, out McpToolDescriptor descriptor) =>
            _tools.TryGetValue(name, out descriptor);

        private void ScanAssembly(Assembly asm)
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }

            foreach (var type in types)
            {
                if (type.GetCustomAttribute<McpServerToolTypeAttribute>() == null)
                    continue;

                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
                    if (toolAttr == null) continue;

                    var descriptor = new McpToolDescriptor
                    {
                        Name = toolAttr.Name ?? ToSnakeCase(method.Name),
                        Description = toolAttr.Description ?? string.Empty,
                        Method = method,
                        Parameters = method.GetParameters(),
                    };
                    descriptor.InputSchema = JsonSchemaBuilder.BuildSchemaForParameters(descriptor.Parameters);

                    if (_tools.ContainsKey(descriptor.Name))
                    {
                        Log.Logger.Warning("MCP tool name collision: {name} (skipping {type}.{method})",
                            descriptor.Name, type.FullName, method.Name);
                        continue;
                    }
                    _tools[descriptor.Name] = descriptor;
                    Log.Logger.Debug("Registered MCP tool {name} from {type}.{method}",
                        descriptor.Name, type.FullName, method.Name);
                }
            }
        }

        public async Task<object> InvokeAsync(McpToolDescriptor descriptor, JObject arguments)
        {
            var args = new object[descriptor.Parameters.Length];
            for (int i = 0; i < descriptor.Parameters.Length; i++)
            {
                var p = descriptor.Parameters[i];
                JToken argToken = null;
                arguments?.TryGetValue(p.Name, StringComparison.Ordinal, out argToken);

                if (argToken != null && argToken.Type != JTokenType.Null)
                {
                    args[i] = argToken.ToObject(p.ParameterType);
                }
                else if (p.IsOptional)
                {
                    args[i] = p.HasDefaultValue ? p.DefaultValue : GetDefault(p.ParameterType);
                }
                else
                {
                    args[i] = GetDefault(p.ParameterType);
                }
            }

            object instance = null;
            if (!descriptor.Method.IsStatic)
            {
                instance = _container.Resolve(descriptor.Method.DeclaringType);
            }

            var result = descriptor.Method.Invoke(instance, args);

            if (result is Task task)
            {
                await task.ConfigureAwait(false);
                var resultProperty = task.GetType().GetProperty("Result");
                return resultProperty != null && task.GetType().IsGenericType
                    ? resultProperty.GetValue(task)
                    : null;
            }
            return result;
        }

        private static object GetDefault(Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;

        private static string ToSnakeCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var sb = new StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
