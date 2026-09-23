@echo off
rem Builds bin\IgclTelemetryProbe.exe against the IGCL SDK files in source\CapFrameX.IGCL.
rem The C runtime is linked statically, so the exe runs on any machine with an Intel graphics driver.
setlocal

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    echo vswhere.exe not found - install Visual Studio with the C++ desktop workload.
    exit /b 1
)

set "VSDIR="
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -prerelease -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSDIR=%%i"
if not defined VSDIR (
    echo No Visual Studio installation with the x64 C++ tools found.
    exit /b 1
)

call "%VSDIR%\VC\Auxiliary\Build\vcvars64.bat" >nul 2>nul || exit /b 1

set "IGCL=%~dp0..\..\source\CapFrameX.IGCL"
if not exist "%~dp0bin" mkdir "%~dp0bin"
pushd "%~dp0bin"
cl /nologo /EHsc /O2 /MT /W3 /I"%IGCL%" "%~dp0IgclTelemetryProbe.cpp" "%IGCL%\cApiWrapper.cpp" /Fe:IgclTelemetryProbe.exe
set "RESULT=%ERRORLEVEL%"
popd

if not "%RESULT%"=="0" exit /b %RESULT%
echo Built %~dp0bin\IgclTelemetryProbe.exe
