# Repository Guidelines

## Project Structure & Module Organization

- `source/` holds the main C# and C++ projects; the WPF app lives in `source/CapFrameX` and native components live in folders like `source/CapFrameX.Hwinfo`, `source/CapFrameX.FrameView`, `source/CapFrameX.IGCL`, and `source/CapFrameX.ADLX`.
- Tests are in `source/CapFrameX.Test` (MSTest), with fixture files under `source/CapFrameX.Test/TestRecordFiles`.
- Assets and documentation resources live in `images/` and project docs like `PORTABLE_MODE.md` and `README.md` live at the repo root.
- Build and version metadata are in `CapFrameX.sln`, `packages/`, and `version/Version.txt`.

## Build, Test, and Development Commands

- `nuget restore CapFrameX.sln` restores NuGet packages for the full solution.
- `msbuild CapFrameX.sln /p:Configuration=Release /p:Platform=x64` builds the full solution.
- `msbuild source\CapFrameX\CapFrameX.csproj /p:Configuration=Release /p:Platform=x64` builds the main app only.
- `msbuild source\CapFrameXInstaller\CapFrameXInstaller.wixproj /p:Configuration=Release /p:Platform=x64` builds the WiX installer.
- `vstest.console source\CapFrameX.Test\bin\x64\Release\CapFrameX.Test.dll` runs unit tests after building the test project.

See `Jenkinsfile` for the CI build ordering and Visual Studio version settings.

## Coding Style & Naming Conventions

- C# uses spaces for indentation and favors Allman-style braces; `System.*` usings should appear before other namespaces.
- Follow existing naming: PascalCase for types and public members, camelCase for locals and parameters, and `I*` for interfaces.
- Keep `packages.config` and `app.config` consistent when adding dependencies or configuration.

## Testing Guidelines

- Tests use MSTest in `source/CapFrameX.Test`; keep new tests near related feature folders (for example `Data` or `Statistics`).
- Name test files with the `*Test.cs` suffix and add new fixture CSVs to `TestRecordFiles` when needed.
- Prefer x64 test runs because several native components are x64-only.

## Commit & Pull Request Guidelines

- Git history was not accessible in this environment, so follow a short, imperative subject line (for example, "Fix capture session parsing") and add context in the body when needed.
- PRs should explain the change, include repro steps, and provide screenshots for UI/overlay changes. Link related issues if applicable.

## Configuration & Runtime Notes

- Portable mode settings are documented in `PORTABLE_MODE.md`; use `portable.json.sample` as a starting point for local overrides.
- Avoid committing local logs (`logs/`) or generated outputs.
