@echo off
setlocal
cd /d "%~dp0"

set "CI_MODE=0"
if /I "%~1"=="--ci" set "CI_MODE=1"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "%CSC%" (
  echo ERROR: .NET Framework C# compiler not found.
  echo Install or enable .NET Framework 4.x and try again.
  if "%CI_MODE%"=="0" pause
  exit /b 1
)

if not exist "dist" mkdir "dist"

echo Building WheelFix...
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ ^
  /win32manifest:app.manifest ^
  /win32icon:assets\WheelFix.ico ^
  /out:dist\WheelFix.exe ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  src\WheelFilterCore.cs ^
  src\AppSettings.cs ^
  src\DiagnosticLog.cs ^
  src\Localization.cs ^
  src\NativeMouseHook.cs ^
  src\MainForm.cs ^
  src\Program.cs

if errorlevel 1 (
  echo.
  echo BUILD FAILED.
  if "%CI_MODE%"=="0" pause
  exit /b 1
)

copy /y "README.md" "dist\README.md" >nul
copy /y "README.it.md" "dist\README.it.md" >nul
copy /y "LICENSE" "dist\LICENSE" >nul

echo.
echo BUILD OK: %CD%\dist\WheelFix.exe
exit /b 0
