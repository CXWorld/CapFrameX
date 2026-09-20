# Repository Guidelines

## Project Structure & Module Organization
- `PmcReader/` contains the core performance counter reader (C#) plus interop and driver wrappers under `PmcReader/Interop`.
- `PmcReader.TestApp/` is a WPF test harness for manual validation and UI testing.
- `intel-perfmon/` holds JSON/CSV metric and event data used by the reader.
- Root-level plugin glue lives in `PmcReaderSensorPlugin.cs`, `PmcReaderSensorEntry.cs`,
  `PmcReaderInterop.Forwarder.cs` and `PmcReaderLogging.cs`.

## Build, Test, and Development Commands
All projects are SDK-style and target `net10.0-windows`; dependencies come from `PackageReference`,
so `/restore` replaces the former `nuget restore` step.
- `msbuild PmcReader.TestApp.sln /restore /p:Configuration=Release /p:Platform=x64` builds the WPF test app.
- `msbuild PmcReader\\PmcReader.sln /restore /p:Configuration=Release /p:Platform=x64` builds the core reader solution.
- `msbuild CapFrameX.PmcReader.Plugin.csproj /restore /p:Configuration=Release /p:Platform=x64` builds the plugin assembly.
- The plugin compiles a curated subset of the `PmcReader/` sources directly rather than referencing
  `PmcReader.csproj`, because that project is a `WinExe` and pulls in the forms and entry point.
  A source file added under `PmcReader/` that the plugin needs has to be listed in its `.csproj`.
- Run the test app from Visual Studio or `PmcReader.TestApp` output; it requires admin privileges to access the WinRing0 driver.

## Coding Style & Naming Conventions
- C# uses spaces for indentation and Allman-style braces (match existing files).
- Follow .NET naming: PascalCase for types/public members, camelCase for locals/parameters.
- Add NuGet dependencies as `PackageReference`; keep shared versions (`System.Management`,
  `System.Reactive`) in step with the CapFrameX projects to avoid restore-time downgrades.

## Testing Guidelines
- There are no automated unit tests in this repository; validation is primarily manual.
- Use `PmcReader.TestApp` for manual verification of sensor output and UI behavior.
- Prefer x64 builds; interop drivers and native dependencies are x64-only.

## Commit & Pull Request Guidelines
- Use short, imperative commit subjects (e.g., “Fix sensor update interval”).
- Include context in the body if the change is non-obvious or touches hardware/interop logic.
- PRs should describe the change, list repro/validation steps, and note any admin-only or hardware-specific requirements.

## Security & Configuration Notes
- The reader uses low-level drivers (`PmcReader/Interop/WinRing0*.sys`); avoid changing these without a hardware validation plan.
- Event/metric data in `intel-perfmon/` is versioned input; update consistently across related JSON/CSV files.
