@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo ERROR: Could not find 64-bit .NET Framework csc.exe
  echo Expected: %CSC%
  echo Install .NET Framework 4.8 ^(included on Windows 10/11^).
  pause
  exit /b 1
)
if not exist "%~dp0MicLock.cs" (
  echo ERROR: MicLock.cs not found in this folder.
  pause
  exit /b 1
)
if not exist "%~dp0app.ico" (
  echo ERROR: app.ico not found in this folder.
  pause
  exit /b 1
)

echo Stopping MicLock if it is running ...
taskkill /F /IM MicLock.exe >nul 2>&1
ping 127.0.0.1 -n 2 >nul

echo Compiling MicLock.exe ...
"%CSC%" /nologo /optimize+ /t:winexe /platform:x64 /win32icon:"%~dp0app.ico" /out:"%~dp0MicLock.exe" /r:System.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll "%~dp0MicLock.cs"
if errorlevel 1 (
  echo.
  echo Build FAILED.
  pause
  exit /b 1
)

start "" "%~dp0MicLock.exe"
endlocal
exit /b 0
