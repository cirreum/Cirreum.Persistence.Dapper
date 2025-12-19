# Cirreum.Persistence.Dapper

[![NuGet Version](https://img.shields.io/nuget/v/Cirreum.Persistence.Dapper.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Persistence.Dapper/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cirreum.Persistence.Dapper.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Persistence.Dapper/)
[![GitHub Release](https://img.shields.io/github/v/release/cirreum/Cirreum.Persistence.Dapper?style=flat-square&labelColor=1F1F1F&color=FF3B2E)](https://github.com/cirreum/Cirreum.Persistence.Dapper/releases)
[![License](https://img.shields.io/github/license/cirreum/Cirreum.Persistence.Dapper?style=flat-square&labelColor=1F1F1F&color=F2F2F2)](https://github.com/cirreum/Cirreum.Persistence.Dapper/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-003D8F?style=flat-square&labelColor=1F1F1F)](https://dotnet.microsoft.com/)

**Lightweight SQL Server persistence layer using Dapper for .NET applications**

## Overview

**Cirreum.Persistence.Dapper** provides a streamlined SQL Server database connection factory using Dapper. Built to integrate seamlessly with the Cirreum Foundation Framework, it offers flexible authentication options including Azure AD (Entra ID) support for modern cloud-native applications.

## Key Features

- **Connection Factory Pattern** - Clean `IDbConnectionFactory` abstraction for SQL Server connections
- **Azure AD Authentication** - Native support for `DefaultAzureCredential` token-based authentication
- **Multi-Instance Support** - Keyed service registration for multiple database connections
- **Health Checks** - Native ASP.NET Core health check integration with customizable queries
- **Dapper Integration** - Leverage Dapper's performance for data access

## Quick Start

```csharp
// Program.cs - Register with IHostApplicationBuilder
builder.AddDapperSql("default", options => {
    options.ConnectionString = "Server=...;Database=...";
    options.UseAzureAdAuthentication = true;
    options.CommandTimeoutSeconds = 60;
});

// Inject and use the connection factory
public class UserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<User?> GetUserAsync(int id, CancellationToken ct)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE Id = @Id",
            new { Id = id });
    }
}
```

## Configuration

### Programmatic Configuration

```csharp
// Simple connection string
builder.AddDapperSql("default", "Server=localhost;Database=MyDb;Trusted_Connection=true");

// Full configuration
builder.AddDapperSql("default", settings => {
    settings.ConnectionString = "Server=myserver.database.windows.net;Database=MyDb";
    settings.UseAzureAdAuthentication = true;
    settings.CommandTimeoutSeconds = 30;
}, healthOptions => {
    healthOptions.Query = "SELECT 1";
    healthOptions.Timeout = TimeSpan.FromSeconds(5);
});
```

### Multiple Database Instances

```csharp
// Register multiple databases with keyed services
builder.AddDapperSql("primary", "Server=primary.database.windows.net;Database=Main");
builder.AddDapperSql("reporting", "Server=replica.database.windows.net;Database=Reporting");

// Inject specific instance
public class ReportService([FromKeyedServices("reporting")] IDbConnectionFactory factory)
{
    // Uses the reporting database connection
}
```

### appsettings.json Configuration

```json
{
  "ServiceProviders": {
    "Persistence": {
      "Dapper": {
        "default": {
          "Name": "MyPrimary",
          "UseAzureAdAuthentication": true,
          "CommandTimeoutSeconds": 30,
          "HealthOptions": {
            "Query": "SELECT 1",
            "Timeout": "00:00:05"
          }
        }
      }
    }
  }
}
```

The `Name` property is used to resolve the connection string via `Configuration.GetConnectionString(name)`. For production, store connection strings in Azure Key Vault using the naming convention `ConnectionStrings--{Name}` (e.g., `ConnectionStrings--MyPrimary`).

## Azure AD Authentication

When `UseAzureAdAuthentication` is enabled, the connection factory uses `DefaultAzureCredential` to obtain access tokens for Azure SQL Database. This supports:

- Managed Identity (recommended for production)
- Azure CLI credentials (for local development)
- Visual Studio / VS Code credentials
- Environment variables

```csharp
builder.AddDapperSql("default", settings => {
    settings.ConnectionString = "Server=myserver.database.windows.net;Database=MyDb";
    settings.UseAzureAdAuthentication = true;
});
```

## Contribution Guidelines

1. **Be conservative with new abstractions**
   The API surface must remain stable and meaningful.

2. **Limit dependency expansion**
   Only add foundational, version-stable dependencies.

3. **Favor additive, non-breaking changes**
   Breaking changes ripple through the entire ecosystem.

4. **Include thorough unit tests**
   All primitives and patterns should be independently testable.

5. **Document architectural decisions**
   Context and reasoning should be clear for future maintainers.

6. **Follow .NET conventions**
   Use established patterns from Microsoft.Extensions.* libraries.

## Versioning

Cirreum.Persistence.Dapper follows [Semantic Versioning](https://semver.org/):

- **Major** - Breaking API changes
- **Minor** - New features, backward compatible
- **Patch** - Bug fixes, backward compatible

Given its foundational role, major version bumps are rare and carefully considered.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

**Cirreum Foundation Framework**
*Layered simplicity for modern .NET*
