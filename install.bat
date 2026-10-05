@echo off
rem Puzzle Studio - installation apres un clone du depot.
rem Compile le Player Template puis PuzzleStudio.exe dans Build\, et propose un raccourci sur le Bureau.
rem Usage : install.bat        (interactif)
rem         install.bat /y     (sans questions : pas de raccourci, ne lance rien)
setlocal
title Puzzle Studio - installation
cd /d "%~dp0"
set AUTO=0
if /I "%~1"=="/y" set AUTO=1

echo ==========================================================
echo   Puzzle Studio - installation
echo ==========================================================
echo.

rem --- 1. Unity 6000.0.32f1
call "%~dp0tools\find_unity.bat"
if errorlevel 1 goto nounity
echo Unity trouve : %UNITY%

rem --- 2. Le projet ne doit pas etre ouvert dans l'editeur Unity
if exist "%~dp0Temp\UnityLockfile" del "%~dp0Temp\UnityLockfile" >nul 2>&1
if exist "%~dp0Temp\UnityLockfile" (
    echo.
    echo [ERREUR] Le projet est ouvert dans l'editeur Unity. Ferme-le puis relance install.bat.
    goto fail
)

rem --- 3. PuzzleStudio.exe ne doit pas tourner (son dossier va etre reconstruit)
tasklist /FI "IMAGENAME eq PuzzleStudio.exe" 2>nul | findstr /I /C:"PuzzleStudio.exe" >nul
if not errorlevel 1 (
    echo.
    echo [ERREUR] PuzzleStudio.exe est ouvert. Ferme-le puis relance install.bat.
    goto fail
)

rem --- 4. Licence : Unity Hub doit etre ouvert et connecte a un compte Unity
tasklist 2>nul | findstr /I /C:"Unity Hub" >nul
if errorlevel 1 (
    echo.
    echo [ATTENTION] Unity Hub ne semble pas ouvert. Ouvre Unity Hub et connecte-toi
    echo             ^(licence Personal gratuite^), sinon la compilation echoue.
    if "%AUTO%"=="0" pause
)

echo.
echo [1/2] Player Template ^(la premiere fois Unity importe tout le projet : 5 a 15 min^)...
call "%~dp0build.bat" template
if errorlevel 1 goto fail
echo.
echo [2/2] PuzzleStudio...
call "%~dp0build.bat" studio
if errorlevel 1 goto fail

echo.
echo ==========================================================
echo   Installation terminee.
echo   Studio : %~dp0Build\PuzzleStudio\PuzzleStudio.exe
echo ==========================================================
if "%AUTO%"=="1" exit /b 0

echo.
choice /C ON /M "Creer un raccourci Puzzle Studio sur le Bureau"
if errorlevel 2 goto launch
powershell -NoProfile -Command "$l = (New-Object -ComObject WScript.Shell).CreateShortcut([IO.Path]::Combine([Environment]::GetFolderPath('Desktop'), 'Puzzle Studio.lnk')); $l.TargetPath = '%~dp0Build\PuzzleStudio\PuzzleStudio.exe'; $l.WorkingDirectory = '%~dp0Build\PuzzleStudio'; $l.Save()"
echo Raccourci cree.

:launch
choice /C ON /M "Lancer Puzzle Studio maintenant"
if errorlevel 2 exit /b 0
start "" "%~dp0Build\PuzzleStudio\PuzzleStudio.exe"
exit /b 0

:nounity
echo.
echo [ERREUR] Unity %UNITY_VERSION% est introuvable.
echo   1. Installe Unity Hub : https://unity.com/download
echo   2. Installe l'editeur %UNITY_VERSION% ^(le lien ci-dessous l'ouvre dans Unity Hub^) :
echo      unityhub://%UNITY_VERSION%/%UNITY_CHANGESET%
echo   3. Relance install.bat.
echo   Si Unity est installe ailleurs : set UNITY_EXE=C:\chemin\vers\Unity.exe puis install.bat
if "%AUTO%"=="1" exit /b 1
echo.
choice /C ON /M "Ouvrir Unity Hub sur la bonne version maintenant"
if errorlevel 2 exit /b 1
start "" "unityhub://%UNITY_VERSION%/%UNITY_CHANGESET%"
exit /b 1

:fail
echo.
echo Installation interrompue. Les journaux sont dans %~dp0Logs\
if "%AUTO%"=="0" pause
exit /b 1
