-- TechMove database schema for Azure SQL Database
-- In SSMS, connect to the Azure SQL server and select the target database before running.
-- This script is idempotent with respect to the EF Core migration history.

IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]') IS NULL
BEGIN
	CREATE TABLE [dbo].[__EFMigrationsHistory]
	(
		[MigrationId] nvarchar(150) NOT NULL,
		[ProductVersion] nvarchar(32) NOT NULL,
		CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
	);
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS
(
	SELECT 1
	FROM [dbo].[__EFMigrationsHistory]
	WHERE [MigrationId] = N'20261014131224_InitialCreate'
)
BEGIN
	CREATE TABLE [dbo].[Clients]
	(
		[Id] int NOT NULL IDENTITY(1, 1),
		[Name] nvarchar(150) NOT NULL,
		[ContactDetails] nvarchar(250) NOT NULL,
		[Region] nvarchar(100) NOT NULL,
		CONSTRAINT [PK_Clients] PRIMARY KEY ([Id])
	);

	CREATE TABLE [dbo].[Contracts]
	(
		[Id] int NOT NULL IDENTITY(1, 1),
		[ClientId] int NOT NULL,
		[Title] nvarchar(100) NOT NULL,
		[StartDate] datetime2 NOT NULL,
		[EndDate] datetime2 NOT NULL,
		[Status] nvarchar(20) NOT NULL,
		[ServiceLevel] nvarchar(100) NOT NULL,
		[AgreementStoredFileName] nvarchar(260) NULL,
		[AgreementOriginalFileName] nvarchar(260) NULL,
		CONSTRAINT [PK_Contracts] PRIMARY KEY ([Id]),
		CONSTRAINT [FK_Contracts_Clients_ClientId]
			FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id])
			ON DELETE NO ACTION
	);

	CREATE TABLE [dbo].[ServiceRequests]
	(
		[Id] int NOT NULL IDENTITY(1, 1),
		[ContractId] int NOT NULL,
		[Description] nvarchar(1000) NOT NULL,
		[CostUsd] decimal(18,2) NOT NULL,
		[LocalCostZar] decimal(18,2) NOT NULL,
		[ExchangeRate] decimal(18,6) NOT NULL,
		[Status] nvarchar(20) NOT NULL,
		[CreatedAtUtc] datetime2 NOT NULL,
		CONSTRAINT [PK_ServiceRequests] PRIMARY KEY ([Id]),
		CONSTRAINT [FK_ServiceRequests_Contracts_ContractId]
			FOREIGN KEY ([ContractId]) REFERENCES [dbo].[Contracts] ([Id])
			ON DELETE NO ACTION
	);

	CREATE INDEX [IX_Contracts_ClientId]
		ON [dbo].[Contracts] ([ClientId]);

	CREATE INDEX [IX_ServiceRequests_ContractId]
		ON [dbo].[ServiceRequests] ([ContractId]);

	INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
	VALUES (N'20261014131224_InitialCreate', N'10.0.0');
END;
GO

COMMIT;
GO
