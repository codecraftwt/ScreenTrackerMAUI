@echo off
echo ========================================
echo   ScreenTracker MSIX Build
echo ========================================
echo.

cd /d "D:\Snehal\ScreenTracker latest updated UI and backend 27 oct 2025\ScreenTracker1"

echo [1/3] Cleaning...
dotnet clean
echo.

echo [2/3] Generating unique version...
for /f "tokens=1-4 delims=:. " %%a in ("%time%") do set VERSION=1.0.%%a%%b%%c%%d
echo Version: %VERSION%
echo.

echo [3/3] Building MSIX...
dotnet publish -f net8.0-windows10.0.22621.0 -c Release -p:PublishProfile=Properties/PublishProfiles/MSIX-win-x86.pubxml -p:Version=%VERSION%

echo.
echo ========================================
echo   Build Complete!
echo   Version: %VERSION%
echo ========================================
pause
