@echo off
setlocal
cd /d "%~dp0"
call build.cmd
if errorlevel 1 exit /b 1
start "" "%CD%\dist\WheelFix.exe"
