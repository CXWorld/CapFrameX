# Repository Guidelines

## Project Structure & Module Organization
- `PmcReader/` contains the core performance counter reader (C#) plus interop and driver wrappers under `PmcReader/Interop`.
- `PmcReader.TestApp/` is a WPF test harness for manual validation and UI testing.
- `intel-perfmon/` holds JSON/CSV metric and event data used by the reader.
- Root-level plugin glue lives in `PmcReaderSensorPlugin.cs` and `PmcReaderSensorEntry.cs`.
- `packages/` and `packages.config` store NuGet dependencies for the legacy packages.config workflow.

## Build, Test, and Development Commands
- `nuget restore PmcReader.TestApp.sln` restores NuGet packages for the test app and its dependencies.
- `msbuild PmcReader.TestApp.sln /p:Configuration=Release /p:Platform=x64` builds the WPF test app.
- `msbuild PmcReader\\PmcReader.sln /p:Configuration=Release /p:Platform=x64` builds the core reader solution.
- `msbuild CapFrameX.PmcReader.Plugin.csproj /p:Configuration=Release /p:Platform=x64` builds the plugin assembly.
- Run the test app from Visual Studio or `PmcReader.TestApp` output; it requires admin privileges to access the WinRing0 driver.

## Coding Style & Naming Conventions
- C# uses spaces for indentation and Allman-style braces (match existing files).
- Follow .NET naming: PascalCase for types/public members, camelCase for locals/parameters.
- Keep `packages.config` in sync when adding/removing NuGet dependencies.

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
