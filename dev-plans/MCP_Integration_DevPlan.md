# CapFrameX MCP Integration – Development Plan

> **Architecture:** The MCP server is hosted **in-process** inside `CapFrameX.exe` as a new EmbedIO module on the existing local webserver. No second executable, no .NET 9 process, no IPC. When CapFrameX is running, the MCP endpoint is reachable at `http://localhost:<WebservicePort>/mcp`. When CapFrameX is not running, the endpoint is unreachable.

> **Why not the official MCP C# SDK:** That SDK requires .NET 8+ and cannot load into the .NET Framework 4.7.2 WPF process. The foundation document [`MCP_Server_Implementation_NET472_WPF_App.md`](./MCP_Server_Implementation_NET472_WPF_App.md) recommends a separate .NET 9 child process to work around this — but CapFrameX already operates a substantial HTTP API stack (`CapFrameX.ApiInterface` with EmbedIO + WebApi controllers) inside the WPF process. Reusing that infrastructure with a custom MCP protocol implementation is simpler than running a second runtime.

## 1. Goal

Expose CapFrameX functionality as MCP tools to AI clients (Claude Code, Claude Desktop). Server runs only when CapFrameX runs. Users register the URL once in their MCP client and connect on demand.

## 2. Scope

**In scope**
- Read-only analysis of capture records (frametimes, statistics, sensor data).
- Programmatic capture lifecycle (start/stop, status).
- Read/write of `AppSettings.json` and overlay configuration.
- Listing and querying records, processes, and configured directories.

**Out of scope**
- Game automation / input simulation / gameplay control.
- Live screen-vision pipelines.
- Any standalone-without-CapFrameX use mode.
- Cloud webservice MCP exposure (existing `CapFrameX.Webservice.Host` is a separate surface).

## 3. Lifecycle & Connection Model

### Process model

```
┌────────────────────────────────────────────────────────────┐
│ CapFrameX.exe (.NET Framework 4.7.2, WPF)                  │
│                                                            │
│  Bootstrapper ─► DI container                              │
│         │                                                  │
│         ▼                                                  │
│  WebserverFactory.CreateWebServer(...)                     │
│         │                                                  │
│         ├─► WithWebApi("/api", ...)        existing REST   │
│         ├─► WithModule(/ws/osd ...)        existing WS     │
│         ├─► WithModule(/ws/sensors ...)    existing WS     │
│         └─► WithModule(McpModule "/mcp")   NEW             │
│                                                            │
│  EmbedIO listens on http://*:<WebservicePort>              │
└────────────────────────────────────────────────────────────┘
                          ▲
                          │ HTTP + JSON-RPC 2.0
                          │ (POST → SSE response stream)
                          ▼
                   ┌───────────────┐
                   │  Claude Code  │
                   │  (or Claude   │
                   │   Desktop)    │
                   └───────────────┘
```

### Connection model (Claude Code side)

One-time setup by the user:

```bash
claude mcp add capframex --transport http http://localhost:<port>/mcp
```

`<port>` is whatever EmbedIO ended up binding to (`IAppConfiguration.WebservicePort`, default determined at first run). The user can read it from CapFrameX's status bar / about dialog (a UI affordance to copy the URL is a nice-to-have for Phase 1.5).

In an active Claude Code session:
- `/mcp` lists registered servers and shows connection status.
- The server appears as **disconnected** when `CapFrameX.exe` is not running (HTTP endpoint unreachable).
- The server appears as **connected** when `CapFrameX.exe` is running.

### Security posture

- The existing webserver binds to `http://*` and is reachable on localhost. Same posture applies to the MCP endpoint — no auth, localhost-scoped by user-firewall convention.
- Phase 1's destructive operations (capture start/stop, config writes) rely on Claude Code's per-tool approval prompt.

## 4. Solution Structure

One new project added to `CapFrameX.sln`:

```
CapFrameX.sln
├── ... existing projects ...
└── source/
    └── CapFrameX.Mcp/        net472 library — EmbedIO module + MCP protocol
```

That's the entirety of the new code surface. No `CapFrameX.Mcp.Core`, no .NET 9 anything.

`CapFrameX.Mcp` references:
- `EmbedIO` (existing in `CapFrameX.ApiInterface` package set)
- `Newtonsoft.Json` (existing throughout)
- `CapFrameX.Statistics.NetStandard` and `CapFrameX.Data.Session` (for Phase 1 read-only tools)
- `CapFrameX.Capture.Contracts` (for Phase 2 capture-control tools)
- `CapFrameX.Contracts` (for `IAppConfiguration` etc. in Phase 3)

All of these are net472-compatible.

`CapFrameX.ApiInterface` then references `CapFrameX.Mcp` and registers its module in `WebserverFactory.CreateWebServer`.

## 5. MCP Library Internals

### Public surface — attributes that mirror the official SDK

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class McpServerToolTypeAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class McpServerToolAttribute : Attribute
{
    public string Name { get; set; }       // optional override; default: snake_case method name
    public string Description { get; set; }
}

[AttributeUsage(AttributeTargets.Parameter)]
public class DescriptionAttribute : Attribute  // re-use System.ComponentModel.DescriptionAttribute
{ ... }
```

Rationale: developers who later move to the official SDK should encounter the same authoring model.

### Tool registration

`McpToolRegistry`:
1. At module construction, scan a configurable assembly list for types with `[McpServerToolType]`.
2. For each, find methods with `[McpServerTool]`.
3. Build a `McpToolDescriptor`: name, description, parameter list, JSON schema generated from parameter types, invoker delegate that resolves `this` from the DI container and binds args.
4. Cache descriptors in a dictionary keyed by tool name.

### JSON Schema generation

A small `JsonSchemaBuilder` that handles primitives, nullable types, enums, arrays, and DTO classes via reflection. Output matches MCP's expected `inputSchema` shape:

```json
{
  "type": "object",
  "properties": { "filter": { "type": "string", "description": "..." } },
  "required": []
}
```

Limited scope: covers what Phase 1 tools actually need. Generic-recursive support is out of scope.

### EmbedIO module

`McpModule : WebModuleBase`:
- Handles `POST /mcp` (the only path) with content-type `application/json`.
- Reads the JSON-RPC request envelope (single message; batch optional).
- Dispatches by method:
  - `initialize` → `{protocolVersion, capabilities: {tools: {listChanged: false}}, serverInfo: {name, version}}`
  - `notifications/initialized` → no response (acknowledgement only)
  - `tools/list` → list tool descriptors
  - `tools/call` → resolve tool, deserialize arguments, invoke, wrap result
- Streams response as SSE if the client requests `text/event-stream` in `Accept`, otherwise plain JSON. (Most clients today do request SSE.)

### Dependency injection

Tool classes are resolved from CapFrameX's DryIoc container, which gives MCP tools access to all existing services (`IRecordManager`, `ICaptureService`, `IAppConfiguration`, `IOverlayService`, `ISensorService`, …) without any IPC.

### Logging

Re-uses `Serilog.Log.Logger` — same sink as the rest of CapFrameX. MCP messages and tool invocations log at Information level; tool exceptions at Error.

## 6. Configuration Surface

Add to `IAppConfiguration` and `CapFrameXConfiguration`:

```csharp
bool McpEnabled { get; set; }   // default: true
```

If disabled, `WebserverFactory.CreateWebServer` skips the `WithModule(McpModule)` call. The webserver itself still runs (other API surfaces are independent).

Port: deliberately reuses `IAppConfiguration.WebservicePort` — no new port to manage. The MCP endpoint lives at `/mcp` on the same port as the existing API.

## 7. Phase Plan

### Phase 1 — Skeleton + read-only analysis (3–5 days)

**Skeleton work (initial):**
- New `CapFrameX.Mcp` net472 library with attributes, schema generator, registry, EmbedIO module, JSON-RPC handler.
- Single placeholder tool `cfx_ping` in the same library.
- `WebserverFactory` registers `McpModule` on `/mcp`.
- `McpEnabled` config wired through.

**Read-only tools** (after skeleton):

| Tool | Returns |
|---|---|
| `cfx_list_records(filter?)` | List of `{id, path, game, processName, recordedAt, durationSec, comment}`. |
| `cfx_get_record(id)` | Full session metadata. |
| `cfx_get_metrics(id, metrics[], runIndex?)` | Map of metric name → value. |
| `cfx_get_sensor_summary(id, runIndex?)` | Per-sensor aggregates: avg/min/max for CPU/GPU. |
| `cfx_compare_records(ids[], metrics[]?)` | Side-by-side metric table. |
| `cfx_search_records(query)` | Free-text search across game/comment/hardware. |

**Exit criteria:** A Claude Code session can answer *"compare the last three Cyberpunk records and tell me which one had the most stuttering"* via the registered HTTP MCP server.

### Phase 2 — Live capture control (2–3 days)

| Tool |
|---|
| `cfx_list_processes()` |
| `cfx_start_capture(processName, durationSec?, comment?)` |
| `cfx_stop_capture()` |
| `cfx_get_capture_status()` |
| `cfx_wait_for_capture_completion(timeoutSec)` |

These resolve `ICaptureService` directly from DI — no IPC, no special handling. Phase 2 is small precisely because in-process gives us free access to existing services.

### Phase 3 — Configuration & Overlay (1–2 days)

| Tool |
|---|
| `cfx_get_config(section?)` |
| `cfx_set_config(path, value)` |
| `cfx_get_overlay_config(slot)` |
| `cfx_set_overlay_config(slot, config)` |

Direct DI access to `IAppConfiguration` and `IOverlayService`. Hot-reload of overlay changes happens automatically because we're in-process.

### Phase 4 (later) — Reports & comparison charts

Wrap the existing report/export pipeline. Same process, no new architectural concern — just more tools.

## 8. Build & Distribution

- `CapFrameX.Mcp.csproj` is a normal net472 library, builds with msbuild like every other project.
- Added to `CapFrameX.sln`.
- `CapFrameX.csproj` (or `CapFrameX.ApiInterface.csproj`) gets a `<ProjectReference>` to it; outputs land in `source/CapFrameX/bin/x64/Release/` automatically via MSBuild copy semantics.
- The existing `heat.exe` PreBuildEvent in `CapFrameXInstaller.wixproj` picks up `CapFrameX.Mcp.dll` via its recursive scan — **no installer changes required**.
- The Jenkinsfile **does not need a new stage** — the existing `Build CX` step compiles the whole solution graph that `CapFrameX.csproj` reaches into.
- No .NET runtime prerequisite changes — only the existing .NET Framework 4.7.2 is needed.

## 9. Tool-Surface Summary

| Phase | Tool | Available without CapFrameX? |
|---|---|---|
| 1 | `cfx_ping`, `cfx_list_records`, `cfx_get_record`, `cfx_get_metrics`, `cfx_get_sensor_summary`, `cfx_compare_records`, `cfx_search_records` | no — server lives in CapFrameX.exe |
| 2 | `cfx_list_processes`, `cfx_start_capture`, `cfx_stop_capture`, `cfx_get_capture_status`, `cfx_wait_for_capture_completion` | no |
| 3 | `cfx_get_config`, `cfx_set_config`, `cfx_get_overlay_config`, `cfx_set_overlay_config` | no |

The "Available without CapFrameX?" column is uniformly "no" by design. If the user wants MCP, they open CapFrameX.

## 10. Risks & Open Questions

1. **MCP spec evolution.** We track the spec manually instead of getting updates via NuGet. The spec has been stable since 2024-11-05; revisions are usually backward-compatible with explicit version negotiation. Mitigation: keep a small `ProtocolVersion` constant and log negotiated version per session.
2. **SSE vs JSON response.** Most current MCP clients (Claude Code, Claude Desktop, MCP Inspector) prefer SSE. Implementation must check `Accept` header and frame responses appropriately. If we get this wrong, clients see 4xx and don't fall back gracefully — test against MCP Inspector early.
3. **JSON Schema accuracy.** Tools whose generated schemas don't match what they accept will produce confusing client-side errors. Limit Phase 1 tool parameters to primitives, enums, arrays of primitives, and simple POCO classes. Reject more exotic shapes.
4. **EmbedIO concurrency.** EmbedIO handles each request on a thread-pool thread. Tools that touch shared state (e.g., `ICaptureService` while a capture is running) need to respect the same threading rules as existing controllers — they already do, since we're hitting the same DI singletons.
5. **Permission UX.** Claude Code prompts before each tool call. Document a recommended pre-allow snippet for read-only `cfx_get_*` and `cfx_list_*` tools in the README.
6. **Schema drift in records.** Same finding as before — single-step `SensorData2` migration covers all current record formats.

## 11. Out-of-Scope but Worth Noting

The "live game observation" pipeline (screen capture + VLM) discussed separately is intentionally not part of this plan. It would be a separate MCP server (a different URL the user registers) — not an extension of this one.
