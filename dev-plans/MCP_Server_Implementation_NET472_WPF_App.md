# MCP Server for a .NET Framework 4.7.2 WPF App – Technical Implementation

## Goal

An existing WPF desktop app (.NET Framework 4.7.2) should be controllable by Claude Code via the Model Context Protocol (MCP).

## Constraint

The MCP C# SDK requires .NET 8 or higher and is **not compatible with .NET Framework 4.7.2**. Therefore, the MCP server must be a **separate .NET 9 console project** that accesses the same data as the WPF app.

## Architecture

```
Claude Code  ←── stdio (JSON-RPC 2.0) ──→  MyApp.Mcp.exe       (.NET 9, console)
                                              │
                                              ├── MCP server (headless, no UI)
                                              └── References shared data layer
                                                    │
MyApp.exe (WPF, .NET Framework 4.7.2) ──────────────┘
                                              Both access the same
                                              database / files
```

## Solution Structure

```
MyApp.sln
├── MyApp/                        .NET Framework 4.7.2  (WPF main project)
├── MyApp.Core/                   .NET Standard 2.0     (shared business logic, data access)
└── MyApp.Mcp/                    .NET 9                (MCP server, console app)
```

- **MyApp** – The existing WPF application. References `MyApp.Core`.
- **MyApp.Core** – Shared library targeting **.NET Standard 2.0**, which is compatible with both .NET Framework 4.7.2 and .NET 9. Contains database access, models, repositories, and business logic that both the WPF app and the MCP server need.
- **MyApp.Mcp** – A lightweight .NET 9 console application that hosts the MCP server. References `MyApp.Core` to access the same data layer.

If the shared business logic currently lives inside the WPF project, it needs to be extracted into `MyApp.Core` first. Only code that both projects need should be moved – UI code stays in `MyApp`.

## NuGet Packages (MyApp.Mcp)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="ModelContextProtocol" Version="*-*" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="9.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\MyApp.Core\MyApp.Core.csproj" />
  </ItemGroup>
</Project>
```

## NuGet Packages (MyApp.Core)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
  </PropertyGroup>
</Project>
```

## MCP Server Entry Point (MyApp.Mcp/Program.cs)

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using MyApp.Core;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// Register shared services from MyApp.Core
builder.Services.AddSingleton<MyDatabase>();
builder.Services.AddSingleton<TaskRepository>();

var app = builder.Build();
await app.RunAsync();
```

**Important:** Use `CreateEmptyApplicationBuilder`, not `CreateDefaultBuilder` – otherwise log messages end up on stdout and corrupt the JSON-RPC communication.

## Tool Definitions (MyApp.Mcp/Tools/)

Tools are defined as C# classes with attributes. The SDK automatically generates the JSON schema for MCP tool discovery.

```csharp
using System.ComponentModel;
using ModelContextProtocol;
using MyApp.Core;

[McpServerToolType]
public class MyAppTools
{
    private readonly TaskRepository _repo;

    public MyAppTools(TaskRepository repo) => _repo = repo;

    [McpServerTool, Description("Creates a new task in the app")]
    public async Task<string> CreateTask(
        [Description("Title of the task")] string title,
        [Description("Person the task is assigned to")] string assignee = "")
    {
        var task = await _repo.CreateTask(title, assignee);
        return $"Task #{task.Id} created: {title} → {assignee}";
    }

    [McpServerTool, Description("Lists tasks filtered by status (open, closed, all)")]
    public async Task<List<TaskItem>> ListTasks(
        [Description("Filter status: open, closed, or all")] string status = "open")
    {
        return await _repo.GetTasksByStatus(status);
    }

    [McpServerTool, Description("Returns project statistics")]
    public async Task<ProjectStats> GetStats()
    {
        return await _repo.GetProjectStats();
    }
}
```

### Guidelines for Tool Descriptions

- Every tool and every parameter needs a `[Description]` attribute.
- Descriptions should be clear and unambiguous – Claude selects tools based on these texts.
- List allowed values for parameters (e.g. "open, closed, all") in the description.
- Return values should be serializable objects or strings.

## Registration in Claude Code

```bash
claude mcp add my-app -- "C:/Path/To/MyApp.Mcp.exe"
```

Note: No `--mcp` flag needed since `MyApp.Mcp` is a dedicated MCP server project.

### Verification

Type the slash command `/mcp` in Claude Code. Expected output:

```
MCP Server Status
• my-app: connected
```

If there are issues, start Claude Code with `claude --mcp-debug`.

## Runtime Behavior

| Aspect | Behavior |
|---|---|
| Process start | Claude Code starts `MyApp.Mcp.exe` automatically |
| Process end | Terminated when Claude Code exits |
| UI | None – `MyApp.Mcp` is a pure console app |
| Permission | Claude Code asks for confirmation before each tool call |
| Instances | MCP process and WPF app are separate processes |

## Important Constraint

The MCP server and the WPF app are **separate processes**. They share no in-memory state. Both can access the same database or files, but concurrent write access must be handled properly (e.g. via database transactions or file locking).

If the running WPF instance needs to be controlled remotely (e.g. updating views, triggering UI actions), inter-process communication (IPC) must be set up – for example via Named Pipes or a local HTTP endpoint.

## MCP Protocol Flow (Reference)

```
1. Claude Code starts MyApp.Mcp.exe
2. → initialize        (handshake, protocol version)
3. ← response          (server info, capabilities)
4. → tools/list        (which tools are available?)
5. ← response          (tool names, descriptions, JSON schemas)
6. → tools/call        (invoke tool with arguments)
7. ← response          (result as text/JSON)
8. ... (further calls)
```
