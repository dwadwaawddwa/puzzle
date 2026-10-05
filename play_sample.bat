@echo off
rem Lance le Player Template avec un SamplePack (sans export).
rem Usage : play_sample.bat [CozyPastel|DarkNeon|MinimalWhite]
set PACK=%1
if "%PACK%"=="" set PACK=CozyPastel
if not exist "%~dp0Build\Template\Game.exe" (
    echo Le Player Template n'est pas encore compile : lance d'abord install.bat
    pause
    exit /b 1
)
start "" "%~dp0Build\Template\Game.exe" -pack "%~dp0SamplePacks\%PACK%" -screen-fullscreen 0 -screen-width 1600 -screen-height 900
