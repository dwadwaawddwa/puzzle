@echo off
rem Lance Puzzle Studio (le generateur de jeux).
if not exist "%~dp0Build\PuzzleStudio\PuzzleStudio.exe" (
    echo PuzzleStudio n'est pas encore compile : lance d'abord install.bat
    pause
    exit /b 1
)
start "" "%~dp0Build\PuzzleStudio\PuzzleStudio.exe"
