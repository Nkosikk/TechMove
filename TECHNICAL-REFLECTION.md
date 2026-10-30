# TechMove Technical Reflection

## DevOps and Automated Testing

Automated testing is a release gate rather than only a developer convenience. TechMove's integration tests start the real ASP.NET Core API pipeline in memory, authenticate through `/api/auth/token`, call protected endpoints over `HttpClient`, and assert HTTP status codes and JSON responses. This catches routing, model binding, authentication, serialization, dependency-injection, and persistence regressions that isolated unit tests cannot detect.

In a CI/CD pipeline, `dotnet test` runs before image publication or deployment. A failed authentication check, changed route, invalid response contract, or broken database registration stops the pipeline before the change reaches production. Unit tests still validate focused rules such as agreement-file validation, currency conversion, and contract workflow restrictions. Together, fast unit tests and API integration tests reduce the risk of breaking changes while retaining useful failure diagnostics.

The tests use an isolated EF Core in-memory database and therefore do not modify a developer or production database. Production confidence can be increased further by adding a pipeline stage that runs the same tests against a temporary SQL Server container.

## Service-Oriented Architecture

The database and business logic are owned by `TechMove.Api`. The frontend has no EF Core dependency and communicates only through the authenticated API using a typed `HttpClient`. `TechMove.Contracts` contains transport contracts shared between the two processes without exposing persistence entities. This separation allows the API and presentation layer to be deployed, secured, monitored, and scaled independently.

JWT bearer authentication protects business endpoints. The frontend exchanges configured service credentials for a short-lived token and sends it in the `Authorization` header. Production deployments should inject credentials and signing keys from a secret manager rather than source-controlled configuration.

## Containerization

Docker packages each application with the same .NET 10 runtime and operating-system dependencies in development, testing, and production. This removes differences caused by locally installed SDKs, machine configuration, or missing dependencies—the common “it works on my machine” problem.

Docker Compose defines three services: SQL Server, the backend API, and the frontend web application. They communicate through the private `techmove-network` by service name. Only the ports needed by a developer are published. Named volumes preserve SQL Server data and uploaded agreement PDFs across container recreation. The API waits for the SQL Server health check and applies EF Core migrations with retry logic during startup.

Environment variables override connection strings, internal API URLs, JWT settings, and credentials without rebuilding images. Production deployments must replace all default development passwords and keys, use managed secret storage, terminate TLS at an ingress or reverse proxy, and restrict database port exposure.
