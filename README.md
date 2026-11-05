# TechMove

TechMove is a .NET 10 solution containing a Razor Pages frontend, an ASP.NET Core Web API, shared contracts, xUnit tests, and a SQL Server database.

Docker Compose can build and run the complete application stack.

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Git

The .NET 10 SDK is only required when building or testing outside Docker.

## Run with Docker

From the repository root, where `docker-compose.yml` is located, run:

```powershell
docker compose up --build
```

To run in the background:

```powershell
docker compose up --build -d
```

Docker Compose starts these services:

| Service | Address | Description |
| --- | --- | --- |
| TechMove frontend | http://localhost:8080 | Razor Pages web application |
| TechMove API | http://localhost:8081 | ASP.NET Core backend API |
| SQL Server | localhost:1433 | Application database |

The first startup can take several minutes while Docker downloads the images, builds the applications, initializes SQL Server, and applies database migrations.

## View status and logs

```powershell
docker compose ps
docker compose logs -f
```

Logs can also be viewed for an individual service:

```powershell
docker compose logs -f glms-frontend-web
docker compose logs -f glms-backend-api
docker compose logs -f sql-server-db
```

## Stop the application

Stop and remove the containers while preserving application data:

```powershell
docker compose down
```

Stop the containers and permanently delete their volumes:

```powershell
docker compose down -v
```

> Removing the volumes deletes the Docker-hosted database and uploaded agreement files.

## Configuration

The Compose configuration supports these optional environment variables:

| Variable | Purpose |
| --- | --- |
| `MSSQL_SA_PASSWORD` | SQL Server administrator password |
| `TECHMOVE_JWT_KEY` | JWT signing key used by the API |
| `TECHMOVE_API_USERNAME` | Service account username used by the frontend |
| `TECHMOVE_API_PASSWORD` | Service account password used by the frontend |

PowerShell example:

```powershell
$env:MSSQL_SA_PASSWORD = "Choose-A-Strong-Password!"
$env:TECHMOVE_JWT_KEY = "Choose-A-Long-Random-JWT-Signing-Key"
$env:TECHMOVE_API_USERNAME = "techmove-web"
$env:TECHMOVE_API_PASSWORD = "Choose-A-Strong-Service-Password!"
docker compose up --build
```

The fallback values in `docker-compose.yml` are intended for local development only. Use secure secret management and unique values in production.

## Persistent data

Docker Compose creates two named volumes:

- `techmove-sql-data` stores the SQL Server database.
- `techmove-agreements` stores uploaded agreement files.

The volumes remain after `docker compose down` and are removed only when explicitly deleted, such as with `docker compose down -v`.

## Rebuild after code changes

```powershell
docker compose up --build
```

To rebuild without cached image layers:

```powershell
docker compose build --no-cache
docker compose up
```

## Run tests locally

With the .NET 10 SDK installed, run:

```powershell
dotnet test TechMove.slnx
```

## Troubleshooting

### A port is already in use

Ensure ports `8080`, `8081`, and `1433` are available. List running containers with:

```powershell
docker ps
```

### The API cannot connect to SQL Server

Check the service status and logs:

```powershell
docker compose ps
docker compose logs sql-server-db
docker compose logs glms-backend-api
```

The API waits for the SQL Server health check before starting.

### Reset the Docker environment

Delete the TechMove containers and persistent data, then rebuild:

```powershell
docker compose down -v
docker compose up --build
```
