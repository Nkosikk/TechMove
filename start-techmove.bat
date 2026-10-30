@echo off
setlocal

cd /d "%~dp0"

set "NETWORK=techmove-network"
set "SQL_CONTAINER=techmove-sql"
set "MAILPIT_CONTAINER=techmove-mailpit"
set "API_CONTAINER=techmove-api"
set "WEB_CONTAINER=techmove-web"
set "SQL_PASSWORD=TechMove-Local-2026!"
set "ADMIN_EMAIL=admin@techmove.local"
set "ADMIN_PASSWORD=Admin123!"

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

echo Starting Mailpit email server...
docker container inspect "%MAILPIT_CONTAINER%" >nul 2>&1
if errorlevel 1 (
	docker run -d --name "%MAILPIT_CONTAINER%" --network "%NETWORK%" -p 1025:1025 -p 8025:8025 axllent/mailpit
) else (
	docker start "%MAILPIT_CONTAINER%" >nul
)
if errorlevel 1 goto :error
docker network connect "%NETWORK%" "%MAILPIT_CONTAINER%" >nul 2>&1

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
docker run -d --name "%API_CONTAINER%" --network "%NETWORK%" -p 8081:8080 -e "ConnectionStrings__TechMoveDb=Server=%SQL_CONTAINER%;Database=TechMoveDb;User Id=sa;Password=%SQL_PASSWORD%;TrustServerCertificate=True" -e "Database__MigrateOnStartup=true" -e "AdminSeed__DisplayName=TechMove Manager" -e "AdminSeed__Email=%ADMIN_EMAIL%" -e "AdminSeed__Password=%ADMIN_PASSWORD%" -e "EmailDelivery__Host=%MAILPIT_CONTAINER%" -e "EmailDelivery__Port=1025" -e "EmailDelivery__EnableSsl=false" -e "EmailDelivery__FromAddress=noreply@techmove.local" -e "EmailDelivery__FromName=TechMove" -e "EmailDelivery__PublicAppBaseUrl=http://localhost:8080" techmove-api
if errorlevel 1 goto :error

echo Building web image...
docker build -f TechMove/Dockerfile -t techmove-web .
if errorlevel 1 goto :error

echo Recreating web container...
docker rm -f "%WEB_CONTAINER%" >nul 2>&1
docker run -d --name "%WEB_CONTAINER%" --network "%NETWORK%" -p 8080:8080 -e "TechMoveApi__BaseUrl=http://%API_CONTAINER%:8080/" techmove-web
if errorlevel 1 goto :error

echo Waiting for the TechMove API...
call :wait_for_api
if errorlevel 1 echo WARNING: The API is still initializing. Swagger may need a browser refresh.

echo.
echo TechMove is starting:
echo   Website:    http://localhost:8080
echo   API Swagger: http://localhost:8081/swagger
echo   API Health:  http://localhost:8081/health
echo   Email inbox:  http://localhost:8025
echo   Manager email:  %ADMIN_EMAIL%
echo   Manager password: %ADMIN_PASSWORD%
echo.
echo Use "docker logs techmove-api" if the API needs time to initialize.
echo The containers will continue running after this window closes.
start "" "http://localhost:8080"
start "" "http://localhost:8081/swagger"
start "" "http://localhost:8025"
echo.
pause
exit /b 0

:error
echo.
echo ERROR: TechMove could not be started. Review the Docker output above.
pause
exit /b 1

:wait_for_api
set /a API_WAIT_COUNT=0

:wait_for_api_retry
powershell -NoProfile -Command "try { $response = Invoke-WebRequest -UseBasicParsing -Uri 'http://localhost:8081/health' -TimeoutSec 3; if ($response.StatusCode -eq 200) { exit 0 } }; catch {}; exit 1" >nul 2>&1
if not errorlevel 1 (
	echo TechMove API is ready.
	exit /b 0
)

set /a API_WAIT_COUNT+=1
if %API_WAIT_COUNT% GEQ 30 exit /b 1
timeout /t 2 /nobreak >nul
goto :wait_for_api_retry

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
