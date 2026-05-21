using CapFrameX.Mcp.Protocol;
using CapFrameX.Mcp.Tools;
using DryIoc;
using EmbedIO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Mcp
{
    /// <summary>
    /// EmbedIO module that implements the MCP protocol over HTTP+SSE.
    /// Hosted in-process by CapFrameX.exe alongside the existing REST and
    /// WebSocket modules. Reachable at <c>POST /mcp</c>.
    /// </summary>
    public sealed class McpModule : WebModuleBase
    {
        private readonly McpToolRegistry _registry;
        private readonly string _serverVersion;

        public McpModule(string baseRoute, IContainer container, params Assembly[] toolAssemblies)
            : base(baseRoute)
        {
            _registry = new McpToolRegistry(container, toolAssemblies);
            _serverVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        public override bool IsFinalHandler => true;

        protected override async Task OnRequestAsync(IHttpContext context)
        {
            Log.Logger.Debug("MCP: incoming {method} {path}", context.Request.HttpMethod, context.RequestedPath);

            if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 405;
                return;
            }

            string body;
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            JsonRpcResponse response;
            JToken requestId = null;
            bool isNotification = false;
            try
            {
                var request = JsonConvert.DeserializeObject<JsonRpcRequest>(body);
                if (request == null || string.IsNullOrEmpty(request.Method))
                {
                    response = JsonRpcResponse.ErrorResult(null, McpProtocol.ErrorCodes.InvalidRequest, "Invalid JSON-RPC request");
                }
                else
                {
                    requestId = request.Id;
                    isNotification = request.IsNotification;
                    response = await DispatchAsync(request).ConfigureAwait(false);
                }
            }
            catch (JsonException ex)
            {
                Log.Logger.Warning(ex, "MCP: failed to parse JSON-RPC request body: {body}", body);
                response = JsonRpcResponse.ErrorResult(null, McpProtocol.ErrorCodes.ParseError, "Parse error");
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "MCP: unhandled exception while dispatching request");
                response = JsonRpcResponse.ErrorResult(requestId, McpProtocol.ErrorCodes.InternalError, ex.Message);
            }

            if (isNotification)
            {
                context.Response.StatusCode = 202;
                return;
            }

            await WriteResponseAsync(context, response).ConfigureAwait(false);
        }

        private async Task<JsonRpcResponse> DispatchAsync(JsonRpcRequest request)
        {
            switch (request.Method)
            {
                case McpProtocol.Methods.Initialize:
                    return JsonRpcResponse.OkResult(request.Id, new InitializeResult
                    {
                        ProtocolVersion = McpProtocol.Version,
                        Capabilities = new ServerCapabilities(),
                        ServerInfo = new ServerInfo { Name = McpProtocol.ServerName, Version = _serverVersion },
                    });

                case McpProtocol.Methods.Initialized:
                    // Notification — no response expected; caller handles.
                    return null;

                case McpProtocol.Methods.Ping:
                    return JsonRpcResponse.OkResult(request.Id, new JObject());

                case McpProtocol.Methods.ToolsList:
                    return JsonRpcResponse.OkResult(request.Id, new ToolsListResult
                    {
                        Tools = _registry.GetAll().Select(d => new ToolDefinition
                        {
                            Name = d.Name,
                            Description = d.Description,
                            InputSchema = d.InputSchema,
                        }).ToList(),
                    });

                case McpProtocol.Methods.ToolsCall:
                    return await CallToolAsync(request).ConfigureAwait(false);

                default:
                    return JsonRpcResponse.ErrorResult(request.Id,
                        McpProtocol.ErrorCodes.MethodNotFound,
                        $"Method '{request.Method}' not found");
            }
        }

        private async Task<JsonRpcResponse> CallToolAsync(JsonRpcRequest request)
        {
            ToolsCallParams call;
            try { call = request.Params?.ToObject<ToolsCallParams>(); }
            catch (JsonException ex)
            {
                return JsonRpcResponse.ErrorResult(request.Id, McpProtocol.ErrorCodes.InvalidParams, ex.Message);
            }

            if (call == null || string.IsNullOrEmpty(call.Name))
                return JsonRpcResponse.ErrorResult(request.Id, McpProtocol.ErrorCodes.InvalidParams, "Missing tool name");

            if (!_registry.TryGet(call.Name, out var descriptor))
                return JsonRpcResponse.ErrorResult(request.Id, McpProtocol.ErrorCodes.MethodNotFound, $"Tool '{call.Name}' not found");

            try
            {
                var rawResult = await _registry.InvokeAsync(descriptor, call.Arguments).ConfigureAwait(false);
                var text = rawResult is string s ? s : JsonConvert.SerializeObject(rawResult);
                return JsonRpcResponse.OkResult(request.Id, new ToolsCallResult
                {
                    Content = { ToolContent.FromText(text ?? string.Empty) },
                });
            }
            catch (TargetInvocationException tie)
            {
                Log.Logger.Error(tie.InnerException ?? tie, "MCP tool {name} threw", call.Name);
                return JsonRpcResponse.OkResult(request.Id, new ToolsCallResult
                {
                    Content = { ToolContent.FromText((tie.InnerException ?? tie).Message) },
                    IsError = true,
                });
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "MCP tool {name} threw", call.Name);
                return JsonRpcResponse.OkResult(request.Id, new ToolsCallResult
                {
                    Content = { ToolContent.FromText(ex.Message) },
                    IsError = true,
                });
            }
        }

        private static async Task WriteResponseAsync(IHttpContext context, JsonRpcResponse response)
        {
            string body = JsonConvert.SerializeObject(response);
            string accept = context.Request.Headers["Accept"] ?? string.Empty;
            bool wantsSse = accept.IndexOf("text/event-stream", StringComparison.OrdinalIgnoreCase) >= 0;

            byte[] payload;
            if (wantsSse)
            {
                context.Response.ContentType = "text/event-stream";
                context.Response.Headers["Cache-Control"] = "no-cache";
                payload = Encoding.UTF8.GetBytes("event: message\ndata: " + body + "\n\n");
            }
            else
            {
                context.Response.ContentType = "application/json";
                payload = Encoding.UTF8.GetBytes(body);
            }

            context.Response.ContentLength64 = payload.Length;
            await context.Response.OutputStream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
            await context.Response.OutputStream.FlushAsync().ConfigureAwait(false);
        }
    }
}
