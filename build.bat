@echo off
rem Puzzle Studio - builds en ligne de commande (fermer l'editeur Unity avant, Unity Hub ouvert).
rem Usage : build.bat template ^| studio ^| samples ^| setup ^| test ^| all
setlocal
call "%~dp0tools\find_unity.bat"
if errorlevel 1 (
    echo Unity %UNITY_VERSION% introuvable. Lance install.bat pour les instructions,
    echo ou definis UNITY_EXE=C:\chemin\vers\Unity.exe
    exit /b 1
)
set PROJ=%~dp0
set PROJ=%PROJ:~0,-1%
if not exist "%PROJ%\Logs" mkdir "%PROJ%\Logs"
set TARGET=%1
if "%TARGET%"=="" set TARGET=all

if /I "%TARGET%"=="setup"    goto setup
if /I "%TARGET%"=="samples"  goto samples
if /I "%TARGET%"=="template" goto template
if /I "%TARGET%"=="studio"   goto studio

if /I "%TARGET%"=="test"     goto test
if /I "%TARGET%"=="all"      goto all
echo Cible inconnue : %TARGET%
exit /b 1

:setup
"%UNITY%" -batchmode -projectPath "%PROJ%" -executeMethod PuzzleStudio.EditorTools.BuildTools.ProjectSetup.RunBatch -logFile "%PROJ%\Logs\setup.log"
goto end

:samples
"%UNITY%" -batchmode -projectPath "%PROJ%" -executeMethod PuzzleStudio.EditorTools.BuildTools.BuildMenu.GenerateSamplesBatch -logFile "%PROJ%\Logs\samples.log"
goto end

:template
"%UNITY%" -batchmode -projectPath "%PROJ%" -executeMethod PuzzleStudio.EditorTools.BuildTools.BuildMenu.BuildTemplateBatch -logFile "%PROJ%\Logs\build-template.log"
goto end

:studio
"%UNITY%" -batchmode -projectPath "%PROJ%" -executeMethod PuzzleStudio.EditorTools.BuildTools.BuildMenu.BuildStudioBatch -logFile "%PROJ%\Logs\build-studio.log"
goto end

:test
"%UNITY%" -batchmode -projectPath "%PROJ%" -runTests -testPlatform EditMode -testResults "%PROJ%\Logs\editmode-results.xml" -logFile "%PROJ%\Logs\editmode.log"
if errorlevel 1 goto end
"%UNITY%" -batchmode -projectPath "%PROJ%" -runTests -testPlatform PlayMode -testResults "%PROJ%\Logs\playmode-results.xml" -logFile "%PROJ%\Logs\playmode.log"
goto end

:all
call "%~f0" samples || exit /b 1
call "%~f0" test || exit /b 1
call "%~f0" template || exit /b 1
call "%~f0" studio || exit /b 1
goto end

:end
if errorlevel 1 (echo ECHEC - voir Logs\) else (echo OK)
exit /b %errorlevel%
