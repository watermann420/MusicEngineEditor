@echo off
setlocal enabledelayedexpansion

echo ========================================
echo   MusicEngineEditor Build Script
echo ========================================
echo.

:: Check if dotnet is installed
echo [1/5] Checking .NET SDK...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK not found!
    echo Please install .NET 8.0 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)
for /f "tokens=*" %%i in ('dotnet --version') do set DOTNET_VERSION=%%i
echo       .NET SDK %DOTNET_VERSION% found

:: Set configuration
set CONFIG=Debug
if "%1"=="Release" set CONFIG=Release
if "%1"=="release" set CONFIG=Release
echo       Configuration: %CONFIG%

:: Clean if requested
if "%2"=="clean" (
    echo.
    echo [2/5] Cleaning solution...
    pushd %~dp0..\MusicEngine
    dotnet clean -c %CONFIG% >nul 2>&1
    popd
    pushd %~dp0MusicEngineEditor
    dotnet clean -c %CONFIG% >nul 2>&1
    popd
    echo       Clean completed
) else (
    echo.
    echo [2/5] Skipping clean
)

:: Restore NuGet packages for MusicEngine
echo.
echo [3/5] Restoring NuGet packages...
echo       Restoring MusicEngine...
pushd %~dp0..\MusicEngine
dotnet restore
if errorlevel 1 (
    echo ERROR: Failed to restore MusicEngine packages
    popd
    pause
    exit /b 1
)
popd
echo       MusicEngine packages restored

:: Restore NuGet packages for MusicEngineEditor
echo       Restoring MusicEngineEditor...
pushd %~dp0MusicEngineEditor
dotnet restore
if errorlevel 1 (
    echo ERROR: Failed to restore MusicEngineEditor packages
    popd
    pause
    exit /b 1
)
popd
echo       MusicEngineEditor packages restored

:: Build MusicEngine
echo.
echo [4/5] Building MusicEngine...
pushd %~dp0..\MusicEngine
dotnet build -c %CONFIG% --no-restore
if errorlevel 1 (
    echo ERROR: MusicEngine build failed
    popd
    pause
    exit /b 1
)
popd
echo       MusicEngine built successfully

:: Build MusicEngineEditor
echo.
echo [5/5] Building MusicEngineEditor...
pushd %~dp0MusicEngineEditor
dotnet build -c %CONFIG% --no-restore
if errorlevel 1 (
    echo ERROR: MusicEngineEditor build failed
    popd
    pause
    exit /b 1
)
popd
echo       MusicEngineEditor built successfully

echo.
echo ========================================
echo   Build completed successfully!
echo ========================================
echo.
echo Output: %~dp0MusicEngineEditor\bin\%CONFIG%\net10.0-windows\
echo.

:: Run if requested
if "%3"=="run" (
    echo Starting MusicEngineEditor...
    start "" "%~dp0MusicEngineEditor\bin\%CONFIG%\net10.0-windows\MusicEngineEditor.exe"
)

if "%1"=="" pause
