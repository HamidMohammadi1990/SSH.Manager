@echo off
cd /d "%~dp0"
dotnet restore SshManager.sln
dotnet build SshManager.sln -c Release
if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build succeeded. Run with:
    echo   dotnet run --project SshManager\SshManager.csproj
)
pause
