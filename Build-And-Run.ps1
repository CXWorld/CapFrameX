[CmdletBinding()]
param(
    [switch]$InstallPrerequisites
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$env:MSBUILDDISABLENODEREUSE = "1"
$env:DOTNET_CLI_USE_MSBUILD_SERVER = "0"

$repositoryRoot = $PSScriptRoot
$vswherePath = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
$installerPath = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\setup.exe"
$appProject = Join-Path $repositoryRoot "source\CapFrameX\CapFrameX.csproj"
$appPath = Join-Path $repositoryRoot "source\CapFrameX\bin\x64\Release\CapFrameX.exe"
$nugetPath = Join-Path $repositoryRoot "source\CapFrameX.Charts\WpfView\nuget.exe"

$stateHashProvider = [Security.Cryptography.SHA256]::Create()
try
{
    $stateKeyBytes = [Text.Encoding]::UTF8.GetBytes([IO.Path]::GetFullPath($repositoryRoot).ToUpperInvariant())
    $stateKey = ([BitConverter]::ToString($stateHashProvider.ComputeHash($stateKeyBytes), 0, 8)).Replace("-", "")
}
finally
{
    $stateHashProvider.Dispose()
}
$processStatePath = Join-Path ([IO.Path]::GetTempPath()) "CapFrameX.BuildAndRun.$stateKey.state"

function Get-VisualStudioInstances
{
    if (-not (Test-Path -LiteralPath $vswherePath))
    {
        throw "Visual Studio Installer was not found. Install Visual Studio Build Tools and rerun this command."
    }

    return @(& $vswherePath -all -products * -format json | ConvertFrom-Json)
}

function Get-V143BuildInstance
{
    foreach ($instance in (Get-VisualStudioInstances | Sort-Object installationVersion -Descending))
    {
        $msbuildPath = Join-Path $instance.installationPath "MSBuild\Current\Bin\MSBuild.exe"
        $toolsRoot = Join-Path $instance.installationPath "VC\Tools\MSVC"
        $v143Targets = Join-Path $instance.installationPath "MSBuild\Microsoft\VC\v170"

        if (-not (Test-Path -LiteralPath $msbuildPath) -or
            -not (Test-Path -LiteralPath $toolsRoot) -or
            -not (Test-Path -LiteralPath $v143Targets))
        {
            continue
        }

        $toolsets = Get-ChildItem -LiteralPath $toolsRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object {
                $version = [Version]$_.Name
                $version.Major -eq 14 -and $version.Minor -ge 30 -and $version.Minor -lt 50
            } |
            Sort-Object { [Version]$_.Name } -Descending

        foreach ($toolset in $toolsets)
        {
            $hasCompiler = Test-Path -LiteralPath (Join-Path $toolset.FullName "bin\Hostx64\x64\cl.exe")
            $hasMfc = Test-Path -LiteralPath (Join-Path $toolset.FullName "atlmfc\include\afx.h")
            $hasMfcLibraries = Test-Path -LiteralPath (Join-Path $toolset.FullName "atlmfc\lib\x64\mfc140.lib")
            $hasCli = Test-Path -LiteralPath (Join-Path $toolset.FullName "lib\x64\msvcmrt.lib")

            if ($hasCompiler -and $hasMfc -and $hasMfcLibraries -and $hasCli)
            {
                return [PSCustomObject]@{
                    InstallationPath = $instance.installationPath
                    InstallationVersion = $instance.installationVersion
                    MsBuildPath = $msbuildPath
                    ToolsetVersion = $toolset.Name
                }
            }
        }
    }

    return $null
}

function Install-V143Prerequisites
{
    if (-not (Test-Path -LiteralPath $installerPath))
    {
        throw "Visual Studio Installer was not found at '$installerPath'."
    }

    $targetInstance = Get-VisualStudioInstances |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.installationPath "MSBuild\Current\Bin\MSBuild.exe") } |
        Sort-Object installationVersion -Descending |
        Select-Object -First 1

    if ($null -eq $targetInstance)
    {
        throw "No Visual Studio instance with MSBuild was found."
    }

    if (Get-Process -Name MSBuild -ErrorAction SilentlyContinue)
    {
        throw "MSBuild is currently running. Close Visual Studio/build processes and run this same command again."
    }

    Write-Host "Installing the v143 x64 compiler, MFC, and C++/CLI prerequisites..." -ForegroundColor Cyan
    $arguments = 'modify --installPath "' + $targetInstance.installationPath +
        '" --add Microsoft.VisualStudio.Component.VC.14.44.17.14.x86.x64' +
        ' --add Microsoft.VisualStudio.Component.VC.14.44.17.14.MFC' +
        ' --add Microsoft.VisualStudio.Component.VC.14.44.17.14.CLI.Support' +
        ' --quiet --norestart'

    $installer = Start-Process -FilePath $installerPath -Verb RunAs -ArgumentList $arguments -Wait -PassThru
    if ($installer.ExitCode -ne 0 -and $installer.ExitCode -ne 3010)
    {
        throw "Visual Studio prerequisite installation failed with exit code $($installer.ExitCode)."
    }
}

function Invoke-NativeCommand
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "'$FilePath' failed with exit code $LASTEXITCODE."
    }
}

function Test-IsAdministrator
{
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Stop-RunningOutputProcesses
{
    $outputDirectory = Split-Path $appPath
    $ownedExecutablePaths = @(
        [IO.Path]::GetFullPath($appPath),
        [IO.Path]::GetFullPath((Join-Path $outputDirectory "PresentMon\PresentMon-2.4.1-x64.exe"))
    )

    if (Test-Path -LiteralPath $processStatePath)
    {
        $removeProcessState = $true
        try
        {
            $stateParts = [IO.File]::ReadAllText($processStatePath).Split('|')
            $trackedProcessId = 0
            $trackedStartTimeTicks = 0L
            if ($stateParts.Length -eq 2 -and
                [int]::TryParse($stateParts[0], [ref]$trackedProcessId) -and
                [long]::TryParse($stateParts[1], [ref]$trackedStartTimeTicks))
            {
                $trackedProcess = Get-Process -Id $trackedProcessId -ErrorAction SilentlyContinue
                if ($null -ne $trackedProcess -and
                    $trackedProcess.ProcessName -eq "CapFrameX" -and
                    $trackedProcess.StartTime.ToUniversalTime().Ticks -eq $trackedStartTimeTicks)
                {
                    $trackedChildProcessIds = @(Get-CimInstance Win32_Process `
                        -Filter "ParentProcessId = $trackedProcessId" -ErrorAction SilentlyContinue |
                        Where-Object { $_.Name -eq "PresentMon-2.4.1-x64.exe" } |
                        Select-Object -ExpandProperty ProcessId)
                    $trackedProcessIds = @($trackedProcessId) + $trackedChildProcessIds
                    try
                    {
                        Stop-Process -Id $trackedProcessIds -Force -ErrorAction Stop
                    }
                    catch
                    {
                        if (-not (Test-IsAdministrator))
                        {
                            $stopArguments = '-NoProfile -Command "Stop-Process -Id ' +
                                ($trackedProcessIds -join ',') + ' -Force -ErrorAction Stop"'
                            $elevatedStop = Start-Process -FilePath "powershell.exe" -Verb RunAs -WindowStyle Hidden `
                                -ArgumentList $stopArguments -Wait -PassThru
                            if ($elevatedStop.ExitCode -ne 0)
                            {
                                throw "Unable to stop the tracked CapFrameX process (PID $trackedProcessId)."
                            }
                        }
                        else
                        {
                            throw
                        }
                    }

                    foreach ($processId in $trackedProcessIds)
                    {
                        $processToWait = Get-Process -Id $processId -ErrorAction SilentlyContinue
                        if ($null -ne $processToWait)
                        {
                            $null = $processToWait.WaitForExit(5000)
                        }
                    }
                }
            }
        }
        catch
        {
            $removeProcessState = $false
            throw
        }
        finally
        {
            if ($removeProcessState)
            {
                Remove-Item -LiteralPath $processStatePath -Force -ErrorAction SilentlyContinue
            }
        }
    }

    $processes = Get-CimInstance Win32_Process -Filter "Name = 'CapFrameX.exe' OR Name = 'PresentMon-2.4.1-x64.exe'" -ErrorAction SilentlyContinue
    foreach ($process in $processes)
    {
        if ($process.ExecutablePath -and $ownedExecutablePaths -contains [IO.Path]::GetFullPath($process.ExecutablePath))
        {
            Stop-Process -Id $process.ProcessId -Force -ErrorAction Stop
        }
    }

}

Push-Location $repositoryRoot
try
{
    $buildInstance = Get-V143BuildInstance
    if ($null -eq $buildInstance)
    {
        if (-not $InstallPrerequisites)
        {
            throw "The v143 x64 compiler, MFC, or C++/CLI support is missing. Rerun with -InstallPrerequisites to let Visual Studio Installer add the required components."
        }

        Install-V143Prerequisites
        $buildInstance = Get-V143BuildInstance
    }

    if ($null -eq $buildInstance)
    {
        throw "The v143 compiler, MFC, or C++/CLI support is still unavailable after installation."
    }

    if (-not (Test-Path -LiteralPath $nugetPath))
    {
        throw "The repository NuGet executable was not found at '$nugetPath'."
    }

    $null = Stop-RunningOutputProcesses

    Write-Host "Using MSVC $($buildInstance.ToolsetVersion) and restoring dependencies..." -ForegroundColor Cyan
    Invoke-NativeCommand -FilePath $nugetPath -Arguments @(
        "restore",
        (Join-Path $repositoryRoot "CapFrameX.sln"),
        "-NonInteractive",
        "-Verbosity", "quiet"
    )

    Invoke-NativeCommand -FilePath "dotnet" -Arguments @(
        "restore",
        $appProject,
        "--runtime", "win-x64",
        "--property:Platform=x64",
        "--verbosity", "quiet"
    )

    Write-Host "Building CapFrameX x64 Release..." -ForegroundColor Cyan
    # The legacy native projects share PDB writers; single-node, non-reused MSBuild avoids intermittent C1041/mspdbsrv contention.
    Invoke-NativeCommand -FilePath $buildInstance.MsBuildPath -Arguments @(
        $appProject,
        "/t:Build",
        "/p:Configuration=Release",
        "/p:Platform=x64",
        "/p:RuntimeIdentifier=win-x64",
        "/m:1",
        "/nr:false",
        "/nologo",
        "/v:minimal",
        "/clp:ErrorsOnly;Summary"
    )

    if (-not (Test-Path -LiteralPath $appPath))
    {
        throw "Build completed but '$appPath' was not produced."
    }

    $process = Start-Process -FilePath $appPath -WorkingDirectory (Split-Path $appPath) -PassThru
    [IO.File]::WriteAllText($processStatePath,
        "$($process.Id)|$($process.StartTime.ToUniversalTime().Ticks)", [Text.Encoding]::UTF8)
    Write-Host "CapFrameX is running (PID $($process.Id))." -ForegroundColor Green
}
finally
{
    try
    {
        $dotnetCommand = Get-Command "dotnet" -ErrorAction SilentlyContinue
        if ($null -ne $dotnetCommand)
        {
            & $dotnetCommand.Source build-server shutdown *> $null
        }
    }
    catch
    {
        Write-Verbose "Unable to shut down the .NET build server: $($_.Exception.Message)"
    }
    finally
    {
        Pop-Location
    }
}

exit 0
