@echo off
rem Cherche l'editeur Unity 6000.0.32f1 et met son chemin dans la variable UNITY.
rem Ordre : variable UNITY_EXE, dossiers par defaut de Unity Hub, dossier perso choisi dans Unity Hub.
rem Retourne errorlevel 1 si rien n'est trouve.
set "UNITY_VERSION=6000.0.32f1"
set "UNITY_CHANGESET=b2e806cf271c"
set "UNITY="

if defined UNITY_EXE if exist "%UNITY_EXE%" set "UNITY=%UNITY_EXE%"
if defined UNITY goto found

for %%D in ("%ProgramFiles%\Unity\Hub\Editor" "%ProgramW6432%\Unity\Hub\Editor" "C:\Unity\Hub\Editor" "D:\Unity\Hub\Editor" "D:\Program Files\Unity\Hub\Editor" "E:\Unity\Hub\Editor") do (
    if not defined UNITY if exist "%%~D\%UNITY_VERSION%\Editor\Unity.exe" set "UNITY=%%~D\%UNITY_VERSION%\Editor\Unity.exe"
)
if defined UNITY goto found

rem Dossier d'installation personnalise (Unity Hub > Preferences > Installs)
set "HUBCFG=%APPDATA%\UnityHub\secondaryInstallPath.json"
if not exist "%HUBCFG%" goto notfound
set "HUBDIR="
for /f "usebackq delims=" %%P in (`powershell -NoProfile -Command "Get-Content -Raw '%HUBCFG%' | ConvertFrom-Json"`) do set "HUBDIR=%%P"
if not defined HUBDIR goto notfound
if exist "%HUBDIR%\%UNITY_VERSION%\Editor\Unity.exe" set "UNITY=%HUBDIR%\%UNITY_VERSION%\Editor\Unity.exe"
if defined UNITY goto found

:notfound
exit /b 1

:found
exit /b 0
