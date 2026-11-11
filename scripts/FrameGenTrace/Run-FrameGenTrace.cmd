@echo off
rem Starts the CapFrameX frame generation trace. Windows asks for administrator rights because recording an ETW trace needs them.
if not exist "%~dp0FrameGenTrace.ps1" (
    echo FrameGenTrace.ps1 was not found next to this file.
    echo Please extract the whole zip file first, then start Run-FrameGenTrace.cmd from the extracted folder.
    pause
    exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0FrameGenTrace.ps1" %*
