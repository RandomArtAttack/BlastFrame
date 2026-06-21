@echo off
title Unity Jiggler - close this window to stop
REM Double-click to start the Unity MCP jiggler. %~dp0 = this file's own folder,
REM so it finds unity-jiggler.ps1 next to it no matter where it's launched from.
REM -ExecutionPolicy Bypass avoids the "running scripts is disabled" block.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0unity-jiggler.ps1"
echo.
echo Jiggler stopped. Press any key to close.
pause >nul
