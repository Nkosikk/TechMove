# TechMove

TechMove is a Global Logistics Management System (GLMS) for managing clients, freight contracts, signed agreements, and service requests. It stores operational data in SQL Server, keeps uploaded agreement PDFs outside the public web root, and converts service-request costs from USD to ZAR using a live exchange rate.

## Features

- **Client management** — create and update clients, contact details, and operating regions.
- **Contract management** — associate contracts with clients and track dates, service levels, and status.
- **Contract filtering** — filter contracts by status and overlapping date range.
- **Agreement storage** — upload, replace, and download signed PDF agreements.
- **Upload validation** — enforce the `.pdf` extension, PDF file signature, and configured size limit.
- **Service requests** — create requests against eligible contracts and update request status.
- **Workflow rules** — prevent new service requests for `Expired` or `OnHold` contracts.
- **Currency conversion** — retrieve the current USD/ZAR rate and store both the rate and calculated ZAR cost.
- **Responsive interface** — Bootstrap-based UI with clear active navigation and application-specific styling.

## Technology stack

| Area | Technology |
| --- | --- |
| Runtime | .NET 10 |
| Web framework | ASP.NET Core MVC with Razor views |
| Data access | Entity Framework Core 10 |
| Database | SQL Server / SQL Server LocalDB / Azure SQL |
| Front end | Razor, Bootstrap, jQuery validation |
| Testing | xUnit, Microsoft Test SDK, Coverlet |
| External API | [Frankfurter](https://www.frankfurter.app/) for USD/ZAR rates |

## Solution structure

```text
TechMove.slnx
├── TechMove/                         ASP.NET Core web application
│   ├── Controllers/                  Client, contract, and request endpoints
│   ├── Data/                         EF Core DbContext and migrations
│   ├── Database/                     Azure SQL initialization script
│   ├── Models/                       Entities, enums, and form view models
│   ├── Services/                     Currency, upload, and workflow services
│   ├── Views/                        Razor views and shared layout
│   ├── wwwroot/                      CSS, JavaScript, and client libraries
│   ├── Program.cs                    Dependency injection and request pipeline
│   └── appsettings.json              Default application configuration
└── TechMove.Tests/                   xUnit unit tests
```

## Domain model

- A **Client** has many **Contracts**.
- A **Contract** belongs to one client and has many **Service Requests**.
- Deleting related records is restricted at the database level to protect dependent data.
- Contract statuses are `Draft`, `Active`, `Expired`, and `OnHold`.
- Service-request statuses are `New`, `InProgress`, `Completed`, and `Cancelled`.
- Enum values are stored as readable strings in SQL Server.

## Prerequisites

- Visual Studio 2026 with the **ASP.NET and web development** workload
- .NET 10 SDK
- SQL Server LocalDB for the default development configuration, or another SQL Server-compatible database
- Internet access when creating a service request, so the application can retrieve the current exchange rate

## Getting started

### 1. Clone and open the solution

```powershell
git clone https://github.com/Nkosikk/TechMove.git
cd TechMove
```

Open `TechMove.slnx` in Visual Studio and set the `TechMove` project as the startup project.

### 2. Configure the database

The default connection string in `TechMove/appsettings.json` uses SQL Server LocalDB:

```json
"ConnectionStrings": {
  "TechMoveDb": "Server=(localdb)\\mssqllocaldb;Database=TechMoveDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```

For another SQL Server instance, override `ConnectionStrings:TechMoveDb` with a development-specific setting, user secret, or environment variable. Do not commit production credentials.

Apply the included EF Core migration from Visual Studio's Package Manager Console:

```powershell
Update-Database -Project TechMove -StartupProject TechMove
```

Alternatively, with the EF Core command-line tools installed:

```powershell
dotnet ef database update --project TechMove --startup-project TechMove
```

For Azure SQL, `TechMove/Database/AzureSql-InitialCreate.sql` contains an idempotent initial-schema script. Connect to the target database, verify that it is selected, and execute the script. Configure the application with the Azure SQL connection string afterward.

### 3. Run the application

Select the `https` launch profile and start the project in Visual Studio. The default development addresses are:

- `https://localhost:7020`
- `http://localhost:5119`

On first use:

1. Create a client.
2. Create a contract for that client and upload a signed PDF.
3. Create a service request for an eligible contract.
4. Review the stored USD amount, exchange rate, and calculated ZAR amount.

## Configuration

Settings are defined in `TechMove/appsettings.json` and can be overridden by environment-specific configuration, user secrets, or environment variables.

| Setting | Default | Purpose |
| --- | --- | --- |
| `ConnectionStrings:TechMoveDb` | LocalDB `TechMoveDb` | SQL Server connection used by EF Core |
| `CurrencyApi:BaseUrl` | `https://api.frankfurter.app/` | Base URL for exchange-rate requests |
| `AgreementStorage:Path` | `App_Data/Agreements` | Agreement storage path relative to the application content root |
| `AgreementStorage:MaximumFileSizeBytes` | `10485760` | Maximum upload size; default is 10 MiB |

Environment variables use double underscores between configuration sections. For example:

```powershell
$env:ConnectionStrings__TechMoveDb = "<SQL Server connection string>"
$env:AgreementStorage__Path = "D:\TechMoveData\Agreements"
```

### Agreement-file behavior

- New contracts require an agreement PDF.
- Editing a contract can retain the existing agreement or replace it.
- Files are assigned generated names rather than user-supplied storage names.
- The original filename is retained for downloads.
- Files are stored outside `wwwroot` and served through the contract controller.
- A file must have a `.pdf` extension and begin with the `%PDF-` signature.
- Replacing an agreement removes the previous file after the database update succeeds.

For production deployment, configure `AgreementStorage:Path` to use persistent storage and ensure that the application identity has read/write permissions. Back up agreement files separately from the SQL database.

### Currency conversion behavior

When a service request is created, the application requests:

```text
latest?from=USD&to=ZAR
```

The retrieved rate and the ZAR result are stored with the request, preserving the conversion used at creation time. The ZAR amount is rounded to two decimal places using midpoint rounding away from zero. If the API cannot return a valid rate, the request is not saved and the user is asked to try again.

## Database management

EF Core migrations are stored under `TechMove/Data/Migrations`.

To add a migration from Visual Studio's Package Manager Console:

```powershell
Add-Migration <MigrationName> -Project TechMove -StartupProject TechMove
Update-Database -Project TechMove -StartupProject TechMove
```

Review generated migrations before applying them to shared or production databases. The Azure SQL script represents the initial schema only; future schema changes should be deployed through reviewed migrations or updated deployment scripts.

## Testing

The `TechMove.Tests` project currently covers:

- USD-to-ZAR calculations and invalid rates
- Rejection of unsupported agreement files
- Acceptance of files with a PDF signature
- Contract-status rules for service-request creation

Run all tests through Visual Studio Test Explorer or from the solution directory:

```powershell
dotnet test TechMove.slnx
```

To collect cross-platform code coverage:

```powershell
dotnet test TechMove.Tests/TechMove.Tests.csproj --collect:"XPlat Code Coverage"
```

## Security and operational notes

- Form-changing actions use antiforgery validation.
- Entity input is constrained with data-annotation validation.
- Uploaded files are not exposed as static assets.
- Generated storage filenames reduce filename collision and path-manipulation risks.
- The PDF signature check is lightweight validation, not malware scanning. Add content scanning if required by the deployment environment.
- The application currently calls `UseAuthorization` but does not configure user authentication or role-based access. Add an identity provider before exposing administrative functions publicly.
- HTTPS redirection and HSTS are enabled for non-development deployments.
- Do not store secrets in `appsettings.json` or source control.

## Troubleshooting

### The application reports a database connection error

Confirm that LocalDB or the configured SQL Server instance is running, verify `ConnectionStrings:TechMoveDb`, and apply the database migration.

### The database reports that a table does not exist

The schema has not been initialized. Run `Update-Database` or execute the provided Azure SQL initialization script against the correct database.

### A service request cannot be created

- Confirm that the selected contract is not `Expired` or `OnHold`.
- Confirm that the application can reach the configured currency API.
- Verify that `CurrencyApi:BaseUrl` includes a valid absolute URL and trailing slash.

### An agreement upload is rejected

Confirm that the file:

- is not empty;
- has a `.pdf` extension;
- begins with a valid PDF signature;
- does not exceed `AgreementStorage:MaximumFileSizeBytes`.

Also verify that the process can create and write to the configured agreement directory.

## Contributing

1. Create a focused branch from `main`.
2. Keep controllers thin and place reusable workflow or infrastructure logic in services.
3. Add or update tests for behavioral changes.
4. Apply and review any required EF Core migration.
5. Run the complete test suite before opening a pull request.
6. Avoid committing database credentials, generated agreement files, build output, or test results.

## License

No project-level license file is currently included. Add a license before distributing or reusing the project outside its intended environment.
