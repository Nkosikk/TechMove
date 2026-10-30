@echo off
setlocal

cd /d "%~dp0"

set "NETWORK=techmove-network"
set "SQL_CONTAINER=techmove-sql"
set "API_CONTAINER=techmove-api"
set "WEB_CONTAINER=techmove-web"
set "SQL_PASSWORD=TechMove-Local-2026!"

echo Checking Docker...
docker info >nul 2>&1
if errorlevel 1 (
	echo Docker is not running. Starting Docker Desktop...
	call :start_docker
	if errorlevel 1 goto :error
)

echo Creating Docker network if needed...
docker network inspect "%NETWORK%" >nul 2>&1 || docker network create "%NETWORK%"
if errorlevel 1 goto :error

echo Starting SQL Server...
docker container inspect "%SQL_CONTAINER%" >nul 2>&1
if errorlevel 1 (
	docker run -d --name "%SQL_CONTAINER%" --network "%NETWORK%" -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=%SQL_PASSWORD%" -p 1433:1433 -v techmove-sql-data:/var/opt/mssql mcr.microsoft.com/mssql/server:2022-latest
) else (
	docker start "%SQL_CONTAINER%" >nul
)
if errorlevel 1 goto :error

echo Building API image...
docker build -f TechMove.Api/Dockerfile -t techmove-api .
if errorlevel 1 goto :error

echo Recreating API container...
docker rm -f "%API_CONTAINER%" >nul 2>&1
docker run -d --name "%API_CONTAINER%" --network "%NETWORK%" -p 8081:8080 -e "ConnectionStrings__TechMoveDb=Server=%SQL_CONTAINER%;Database=TechMoveDb;User Id=sa;Password=%SQL_PASSWORD%;TrustServerCertificate=True" -e "Database__MigrateOnStartup=true" techmove-api
if errorlevel 1 goto :error

echo Building web image...
docker build -f TechMove/Dockerfile -t techmove-web .
if errorlevel 1 goto :error

echo Recreating web container...
docker rm -f "%WEB_CONTAINER%" >nul 2>&1
docker run -d --name "%WEB_CONTAINER%" --network "%NETWORK%" -p 8080:8080 -e "TechMoveApi__BaseUrl=http://%API_CONTAINER%:8080/" techmove-web
if errorlevel 1 goto :error

echo.
echo TechMove is starting:
echo   Website:    http://localhost:8080
echo   API Swagger: http://localhost:8081/swagger
echo   API Health:  http://localhost:8081/health
echo.
echo Use "docker logs techmove-api" if the API needs time to initialize.
echo The containers will continue running after this window closes.
start "" "http://localhost:8080"
echo.
pause
exit /b 0

:error
echo.
echo ERROR: TechMove could not be started. Review the Docker output above.
pause
exit /b 1

:start_docker
set "DOCKER_DESKTOP="
if exist "%LOCALAPPDATA%\Programs\DockerDesktop\Docker Desktop.exe" set "DOCKER_DESKTOP=%LOCALAPPDATA%\Programs\DockerDesktop\Docker Desktop.exe"
if exist "%ProgramFiles%\Docker\Docker\Docker Desktop.exe" set "DOCKER_DESKTOP=%ProgramFiles%\Docker\Docker\Docker Desktop.exe"
if not defined DOCKER_DESKTOP (
	echo ERROR: Docker Desktop could not be found.
	exit /b 1
)

start "" "%DOCKER_DESKTOP%"
set /a DOCKER_WAIT_COUNT=0

:wait_for_docker
docker info >nul 2>&1
if not errorlevel 1 (
	echo Docker is ready.
	exit /b 0
)

set /a DOCKER_WAIT_COUNT+=1
if %DOCKER_WAIT_COUNT% GEQ 24 (
	echo ERROR: Docker Desktop did not become ready within two minutes.
	exit /b 1
)

echo Waiting for Docker Desktop... (%DOCKER_WAIT_COUNT%/24)
timeout /t 5 /nobreak >nul
goto :wait_for_docker
