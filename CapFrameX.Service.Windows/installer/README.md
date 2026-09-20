# CapFrameX Service Installer

This WiX installer package installs the CapFrameX Service as a Windows service.

## Features

- **Service Installation**: Installs CapFrameX Service as a Windows service that starts automatically
- **.NET 10 Runtime Check**: Validates that .NET 10 runtime is installed before installation
- **Complete Deployment**: Includes all necessary binaries (service DLLs and native hardware monitoring DLLs)
- **Data Folders**: Creates application data folders for database and logs with proper permissions
- **Automatic Service Recovery**: Configures the service to automatically restart on failure

## Prerequisites

- WiX Toolset v6.0 or later
- .NET 10 SDK
- Visual Studio 2022 or MSBuild

## Building the Installer

1. Build the CapFrameX.Service.Api project in Release configuration:
   ```bash
   dotnet build src\CapFrameX.Service.Api\CapFrameX.Service.Api.csproj -c Release
   ```

2. Build the native C++ DLLs (ADLX, Hwinfo, IGCL) in Release|x64 configuration

3. Build the installer project:
   ```bash
   dotnet build src\CapFrameX.Service.Installer\CapFrameX.Service.Installer.wixproj -c Release
   ```

The installer MSI will be created in the `bin\Release\` directory.

## Installation Locations

- **Service Binaries**: `C:\Program Files\CapFrameX Service\`
- **Database**: `C:\ProgramData\CapFrameX\Service\Database\`
- **Logs**: `C:\ProgramData\CapFrameX\Service\Logs\`

## Service Details

- **Service Name**: CapFrameXService
- **Display Name**: CapFrameX Service
- **Description**: CapFrameX background service for frame capture and hardware monitoring
- **Account**: LocalSystem
- **Startup Type**: Automatic

## Upgrading

The installer supports major upgrades. Installing a newer version will automatically uninstall the previous version while preserving data in ProgramData.

## Uninstallation

The service will be stopped and removed during uninstallation. The installer will also clean up the program files, but application data (database and logs) will be preserved.
