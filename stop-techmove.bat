@echo off
setlocal

cd /d "%~dp0"

echo Checking Docker...
docker info >nul 2>&1
if errorlevel 1 (
	echo ERROR: Docker is not running. There are no accessible containers to stop.
	goto :error
)

echo Stopping TechMove containers...
call :stop_container techmove-web
if errorlevel 1 goto :error
call :stop_container techmove-api
if errorlevel 1 goto :error
call :stop_container techmove-sql
if errorlevel 1 goto :error

echo.
echo All TechMove containers are stopped.
echo The containers and database volume were preserved.
echo Run start-techmove.bat to start everything again.
echo.
pause
exit /b 0

:stop_container
docker container inspect "%~1" >nul 2>&1
if errorlevel 1 (
	echo   %~1 does not exist; skipping.
	exit /b 0
)

docker stop "%~1" >nul
if errorlevel 1 (
	echo ERROR: Could not stop %~1.
	exit /b 1
)

echo   Stopped %~1.
exit /b 0

:error
echo.
echo TechMove containers could not be stopped.
pause
exit /b 1
