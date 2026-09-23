# Bundled BENCHLAB service

`BL_Service.exe` is BENCHLAB Service **2.4.0**, built from
[`BenchLab-io/BENCHLAB.BENCHLAB_Service`](https://github.com/BenchLab-io/BENCHLAB.BENCHLAB_Service)
revision `e481b8f6d23515be2e63ba166ce593ca8992b08e` (tag `v2.4.0`).

The 2026-09-19 build uses .NET SDK 10.0.401, Release, `win-x64`, and a
self-contained compressed single-file publish with native libraries included.
It contains .NET and ASP.NET Core 10.0.12, so this service does not require an
additional ASP.NET runtime installation. CapFrameX itself still requires the
.NET 10 Desktop Runtime x64.

The executable is Authenticode-signed with the CapFrameX publisher's Certum
certificate and a SHA-256 RFC 3161 timestamp. The adjacent `VERSION` identifies
this packaging variant; the executable's product version remains `2.4.0`.

`appsettings.json` follows the upstream 2.4.0 defaults, including the HTTP API
on `127.0.0.1:8585`, and preserves the existing disabled Event Log output for
the bundled child process. CapFrameX connects through the `BenchlabDiscovery`
named pipe. The application starts the service as a child process when needed;
it does not install a Windows service.

Validation: 536 unit tests passed; 7 external firmware-advisory tests were
skipped. The published executable answered `ListDevices` and `GetServiceInfo`
on an isolated discovery pipe. No BENCHLAB device was attached during that
smoke test. See `LICENSE-BENCHLAB-Service.txt` for the upstream license.

To reproduce the unsigned payload from the source checkout:

```powershell
dotnet publish .\BL_Service\BL_Service.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false `
  -o .\publish-capframex
```

Keep Git available on `PATH` for MinVer. Copy the executable, production
configuration and license deliberately; do not ship `appsettings.Development.json`.
Sign the final executable before building the CapFrameX installer.
