@echo off
REM ============================================================
REM  Hack2Hire — Export AI Prompts
REM  Double-click this at the END of the event on each laptop.
REM ============================================================

title Hack2Hire - Exporting AI Prompts...
color 0A

echo.
echo  ==========================================
echo   HACK2HIRE — AI Prompt Extractor
echo  ==========================================
echo.
echo  Laptop: %COMPUTERNAME%
echo  Time  : %DATE% %TIME%
echo.

REM ── Configuration ─────────────────────────────────────────
REM  Edit SHARED_FOLDER to your actual network share path.
REM  Leave it empty ("") to skip copying to the network.
set SHARED_FOLDER=\\SHARED\Hack2Hire

REM  Local output goes to the Desktop.
set OUTPUT_DIR=%USERPROFILE%\Desktop
REM ──────────────────────────────────────────────────────────

echo  [1/3] Checking PowerShell...
powershell -Command "exit 0" >nul 2>&1
if errorlevel 1 (
    echo  ERROR: PowerShell not available.
    pause & exit /b 1
)

echo  [2/3] Extracting Claude Code prompts...
powershell -ExecutionPolicy Bypass -File "%~dp0Extract-ClaudePrompts.ps1" ^
    -OutputDir "%OUTPUT_DIR%" ^
    -SharedFolder "%SHARED_FOLDER%" ^
    -LaptopName "%COMPUTERNAME%"

if errorlevel 1 (
    echo.
    echo  WARNING: Extraction finished with errors. Check messages above.
) else (
    echo.
    echo  [3/3] Done!
)

echo.
echo  Output saved to: %OUTPUT_DIR%
if not "%SHARED_FOLDER%"=="" echo  Shared copy  at: %SHARED_FOLDER%
echo.
echo  Press any key to open the export folder...
pause >nul
explorer "%OUTPUT_DIR%"
