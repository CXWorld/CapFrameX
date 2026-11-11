@echo off
rem Starts the CapFrameX MPO check. Windows asks for administrator rights because PresentMon needs them.
if not exist "%~dp0MpoCheck.ps1" (
    echo MpoCheck.ps1 was not found next to this file.
    echo Please extract the whole zip file first, then start Run-MpoCheck.cmd from the extracted folder.
    pause
    exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0MpoCheck.ps1" %*
