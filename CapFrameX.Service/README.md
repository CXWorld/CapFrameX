# CapFrameX.Service

Backend service for the CapFrameX redesign.

The service owns capture orchestration, monitoring, storage, and frontend bridge APIs. It should run as a normal development host and later as a packaged desktop/service component. Windows-only integrations such as PresentMon, PawnIO, RTSS, and vendor driver APIs must stay behind capability/provider boundaries so the core service can remain portable.

## Architecture

- **CapFrameX.Service.Api**: ASP.NET Core host and localhost API on port 1337.
- **CapFrameX.Service.Contracts**: DTOs shared with the frontend bridge.
- **CapFrameX.Service.Core**: Domain models, events, and interfaces.
- **CapFrameX.Service.Application**: Application orchestration and use cases.
- **CapFrameX.Service.Infrastructure**: Infrastructure services and event bus.
- **CapFrameX.Service.Data**: SQLite/data access layer.
- **CapFrameX.Service.Input**: Input abstraction.
- **CapFrameX.Service.Capture**: PresentMon capture integration; Windows-only capability.
- **CapFrameX.Service.Monitoring**: LibreHardwareMonitor-derived sensor stack; currently Windows-heavy because of PawnIO and vendor APIs.

## Technology Stack

- .NET 10
- ASP.NET Core Web API
- Event-driven architecture
- Microsoft Dependency Injection
- HTTP request/response bridge
- Server-sent events for low-frequency backend events
- Named pipes remain available for specific local data paths

## Getting Started

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run the service (development)
dotnet run --project src/CapFrameX.Service.Api

# Install as Windows Service
sc create CapFrameXService binPath="<path-to-exe>"
```

## API Endpoints

Base URL: `http://localhost:1337`

- `GET /api/health` - Health check endpoint
- `GET /api/app/version` - App/service version metadata
- `GET /api/capabilities` - Backend capability summary
- `GET /api/capture/status` - Capture status placeholder
- `GET /api/records` - Record list placeholder
- `GET /api/events` - Server-sent event stream

## Named Pipe

- **Name**: `CapFrameXPmdData`
- **Purpose**: Real-time power measurement data streaming

## Development Documentation

- `dev-plans/CapFrameX_Service_Redesign_Development_Log.md` - Current backend redesign progress, decisions, verification, and next steps.
- `dev-plans/CEF_Angular_CapFrameX_Next_DevPlan.md` - Target CEF/Angular desktop architecture.
- `src/CapFrameX.Service.Monitoring/PORTING_STATUS.md` - LibreHardwareMonitorLib sync status.
