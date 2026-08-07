@echo off
setlocal
cd /d "%~dp0"

set "CI_MODE=0"
if /I "%~1"=="--ci" set "CI_MODE=1"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "%CSC%" (
  echo ERROR: .NET Framework C# compiler not found.
  if "%CI_MODE%"=="0" pause
  exit /b 1
)

if not exist "dist" mkdir "dist"

"%CSC%" /nologo /target:exe /optimize+ ^
  /out:dist\WheelFilterCoreTests.exe ^
  /reference:System.dll ^
  src\WheelFilterCore.cs ^
  tests\WheelFilterCoreTests.cs

if errorlevel 1 (
  echo TEST BUILD FAILED.
  if "%CI_MODE%"=="0" pause
  exit /b 1
)

dist\WheelFilterCoreTests.exe
if errorlevel 1 (
  echo TESTS FAILED.
  if "%CI_MODE%"=="0" pause
  exit /b 1
)

if "%CI_MODE%"=="0" pause
