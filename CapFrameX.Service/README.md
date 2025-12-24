# CapFrameX.Service

Backend Windows Service for CapFrameX - Performance monitoring and frame capture service.

## Architecture

- **CapFrameX.Service.Api**: Windows Service host + REST API (Port 1337)
- **CapFrameX.Service.Core**: Domain models, events, and interfaces
- **CapFrameX.Service.Application**: Business logic and event handlers
- **CapFrameX.Service.Infrastructure**: Named pipes server (CapFrameXPmdData) and event bus

## Technology Stack

- .NET 10
- ASP.NET Core Web API
- Event-driven architecture
- Microsoft Dependency Injection
- Named Pipes for real-time streaming

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

## Named Pipe

- **Name**: `CapFrameXPmdData`
- **Purpose**: Real-time power measurement data streaming
