@echo off
powershell -ExecutionPolicy Bypass -File "%~dp0build-cytoid-player.ps1" %*
if errorlevel 1 (
    echo Build failed.
    if /I not "%NOPAUSE%"=="1" pause
    exit /b 1
)
echo Build succeeded.
if /I not "%NOPAUSE%"=="1" pause
exit /b 0
