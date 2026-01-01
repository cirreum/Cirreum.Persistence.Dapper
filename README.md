# Cirreum.Persistence.Dapper

[![NuGet Version](https://img.shields.io/nuget/v/Cirreum.Persistence.Dapper.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Persistence.Dapper/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cirreum.Persistence.Dapper.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Persistence.Dapper/)
[![GitHub Release](https://img.shields.io/github/v/release/cirreum/Cirreum.Persistence.Dapper?style=flat-square&labelColor=1F1F1F&color=FF3B2E)](https://github.com/cirreum/Cirreum.Persistence.Dapper/releases)
[![License](https://img.shields.io/github/license/cirreum/Cirreum.Persistence.Dapper?style=flat-square&labelColor=1F1F1F&color=F2F2F2)](https://github.com/cirreum/Cirreum.Persistence.Dapper/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-003D8F?style=flat-square&labelColor=1F1F1F)](https://dotnet.microsoft.com/)

**Lightweight SQL Server persistence layer using Dapper for .NET applications**

## Overview

**Cirreum.Persistence.Dapper** provides a streamlined SQL Server database connection factory using Dapper. Built to integrate seamlessly with the Cirreum Foundation Framework, it offers flexible authentication options including Azure AD (Entra ID) support for modern cloud-native applications.

The library includes Result-oriented extension methods for common data access patterns, including pagination, cursor-based queries, and automatic SQL constraint violation handling.

## Key Features

- **Connection Factory Pattern** - Clean `IDbConnectionFactory` abstraction for SQL Server connections
- **Result Integration** - Extension methods that return `Result<T>` for railway-oriented programming
- **Pagination Support** - Built-in support for offset (`PagedResult<T>`), cursor (`CursorResult<T>`), and slice (`SliceResult<T>`) pagination
- **Constraint Handling** - Automatic conversion of SQL constraint violations to typed Result failures
- **Azure Authentication** - Native support for `DefaultAzureCredential` token-based authentication
- **Multi-Instance Support** - Keyed service registration for multiple database connections
- **Health Checks** - Native ASP.NET Core health check integration with customizable queries

## Quick Start
```csharp
// Program.cs - Register with IHostApplicationBuilder
builder.AddDapperSql("default", options => {
    options.ConnectionString = "Server=...;Database=...";
    options.UseAzureAuthentication = true;
    options.CommandTimeoutSeconds = 60;
});
```

## Query Extensions

All query extensions return `Result<T>` and integrate with the Cirreum Result monad.

### Single Record Queries
```csharp
public async Task<Result<Order>> GetOrderAsync(Guid orderId, CancellationToken ct)
{
    await using var conn = await db.CreateConnectionAsync(ct);
    
    return await conn.QuerySingleAsync<Order>(
        "SELECT * FROM Orders WHERE OrderId = @OrderId",
        new { OrderId = orderId },
        key: orderId,  // Used for NotFoundException if not found
        ct);
}
```

### Collection Queries
```csharp
public async Task<Result<IReadOnlyList<Order>>> GetOrdersAsync(Guid customerId, CancellationToken ct)
{
    await using var conn = await db.CreateConnectionAsync(ct);
    
    return await conn.QueryAnyAsync<Order>(
        "SELECT * FROM Orders WHERE CustomerId = @CustomerId",
        new { CustomerId = customerId },
        ct);
}
```

## Pagination

### Offset Pagination (PagedResult)

Best for smaller datasets with "Page X of Y" UI requirements.
```csharp
public async Task<Result<PagedResult<Order>>> GetOrdersPagedAsync(
    Guid customerId, int pageSize, int pageNumber, CancellationToken ct)
{
    await using var conn = await db.CreateConnectionAsync(ct);
    var offset = (pageNumber - 1) * pageSize;

    // Query 1: Get total count
    var totalCount = await conn.ExecuteScalarAsync<int>(
        "SELECT COUNT(*) FROM Orders WHERE CustomerId = @CustomerId",
        new { CustomerId = customerId });

    // Query 2: Get page data
    return await conn.QueryPagedAsync<Order>(
        """
        SELECT * FROM Orders 
        WHERE CustomerId = @CustomerId
        ORDER BY CreatedAt DESC
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
        """,
        new { CustomerId = customerId, Offset = offset, PageSize = pageSize },
        totalCount, pageSize, pageNumber, ct);
}
```

### Cursor Pagination (CursorResult)

Best for large datasets, infinite scroll, and real-time data where consistency matters.
```csharp
public async Task<Result<CursorResult<Order>>> GetOrdersCursorAsync(
    Guid customerId, int pageSize, string? cursor, CancellationToken ct)
{
    await using var conn = await db.CreateConnectionAsync(ct);
    var decoded = Cursor.Decode<DateTime>(cursor);

    var sql = decoded is null
        ? """
          SELECT TOP (@PageSize) * FROM Orders
          WHERE CustomerId = @CustomerId
          ORDER BY CreatedAt DESC, OrderId DESC
          """
        : """
          SELECT TOP (@PageSize) * FROM Orders
          WHERE CustomerId = @CustomerId
            AND (CreatedAt < @Column OR (CreatedAt = @Column AND OrderId < @Id))
          ORDER BY CreatedAt DESC, OrderId DESC
          """;

    return await conn.QueryCursorAsync<Order, DateTime>(
        sql,
        new { CustomerId = customerId, decoded?.Column, decoded?.Id },
        pageSize,
        o => (o.CreatedAt, o.OrderId),  // Cursor selector
        ct);
}
```

### Slice Queries (SliceResult)

For "preview with expand" scenarios � load an initial batch and indicate if more exist. Not for pagination.
```csharp
public async Task<Result<SliceResult<Order>>> GetRecentOrdersAsync(
    Guid customerId, CancellationToken ct)
{
    await using var conn = await db.CreateConnectionAsync(ct);

    return await conn.QuerySliceAsync<Order>(
        """
        SELECT TOP (@PageSize) * FROM Orders
        WHERE CustomerId = @CustomerId
        ORDER BY CreatedAt DESC
        """,
        new { CustomerId = customerId },
        pageSize: 5,
        ct);
}
```
```razor
@foreach (var order in slice.Items) { ... }

@if (slice.HasMore) {
    <a href="/orders">View All Orders</a>
}
```

**Use cases:**
- Dashboard widgets showing recent items with "View All" link
- Preview cards with "Show More" expansion
- Batch processing where you grab N items at a time

**Not for:**
- Paginating through results (use `PagedResult` or `CursorResult`)
- Infinite scroll (use `CursorResult`)

## Command Extensions

Insert, Update, and Delete extensions automatically handle SQL constraint violations and convert them to appropriate Result failures.

### Insert
```csharp
public async Task<Result<Guid>> CreateOrderAsync(CreateOrder command, CancellationToken ct)
{
    await using var conn = await db.CreateConnectionAsync(ct);
    var orderId = Guid.CreateVersion7();

    return await conn.InsertAsync(
        """
        INSERT INTO Orders (OrderId, CustomerId, Amount, CreatedAt)
        VALUES (@OrderId, @CustomerId, @Amount, @CreatedAt)
        """,
        new { OrderId = orderId, command.CustomerId, command.Amount, CreatedAt = DateTime.UtcNow },
        () => orderId,
        uniqueConstraintMessage: "Order already exists",
        foreignKeyMessage: "Customer not found",
        ct);
}
```

### Update
```csharp
public async Task<Result> UpdateOrderAsync(UpdateOrder command, CancellationToken ct)
{
    await using var conn = await db.CreateConnectionAsync(ct);

    return await conn.UpdateAsync(
        "UPDATE Orders SET Amount = @Amount WHERE OrderId = @OrderId",
        new { command.OrderId, command.Amount },
        key: command.OrderId,  // Returns NotFound if 0 rows affected
        uniqueConstraintMessage: "Order reference already exists",
        foreignKeyMessage: "Customer not found",
        ct);
}
```

### Delete
```csharp
public async Task<Result> DeleteOrderAsync(Guid orderId, CancellationToken ct)
{
    await using var conn = await db.CreateConnectionAsync(ct);

    return await conn.DeleteAsync(
        "DELETE FROM Orders WHERE OrderId = @OrderId",
        new { OrderId = orderId },
        key: orderId,  // Returns NotFound if 0 rows affected
        foreignKeyMessage: "Cannot delete order, it has associated line items",
        ct);
}
```

### Constraint Handling Summary

| Operation | Constraint | Result | HTTP |
|-----------|------------|--------|------|
| INSERT | Unique violation | `AlreadyExistsException` | 409 |
| INSERT | FK violation | `BadRequestException` | 400 |
| UPDATE | No rows affected | `NotFoundException` | 404 |
| UPDATE | Unique violation | `AlreadyExistsException` | 409 |
| UPDATE | FK violation | `BadRequestException` | 400 |
| DELETE | No rows affected | `NotFoundException` | 404 |
| DELETE | FK violation | `ConflictException` | 409 |

## Fluent Transaction Chaining

Chain multiple database operations in a single transaction with railway-oriented error handling. If any operation fails, subsequent operations are skipped and the error propagates.

### Basic Chaining
```csharp
public async Task<Result<OrderDto>> CreateOrderWithItemsAsync(
    CreateOrder command, CancellationToken ct)
{
    await using var conn = await db.CreateConnectionAsync(ct);

    return await conn.ExecuteInTransactionAsync(db =>
        db.GetAsync<CustomerDto>(
            "SELECT * FROM Customers WHERE CustomerId = @Id",
            new { Id = command.CustomerId },
            key: command.CustomerId)
        .ThenInsertAsync(
            "INSERT INTO Orders (OrderId, CustomerId, CreatedAt) VALUES (@OrderId, @CustomerId, @CreatedAt)",
            customer => new { OrderId = command.OrderId, customer.CustomerId, CreatedAt = DateTime.UtcNow })
        .ThenGetAsync<OrderDto>(
            "SELECT * FROM Orders WHERE OrderId = @Id",
            new { Id = command.OrderId },
            key: command.OrderId)
    , ct);
}
```

### Available Chain Methods

**From `DbResult<T>` (typed result):**
- `MapAsync(Func<T, TResult>)` - Transform the value
- `WhereAsync(Func<T, bool>, Exception)` - Filter with predicate
- `ThenAsync(Func<T, Task<Result>>)` - Execute side effect, return non-generic
- `ThenAsync(Func<T, Task<Result<TResult>>>)` - Execute and transform
- `ThenGetAsync<TResult>(...)` - Query single record
- `ThenGetScalarAsync<TResult>(...)` - Query scalar value
- `ThenInsertAsync(...)` - Insert with optional result selector
- `ThenUpdateAsync(...)` - Update with optional result selector
- `ThenDeleteAsync(...)` - Delete record

**From `DbResultNonGeneric` (void result):**
- `ThenAsync(Func<Task<Result>>)` - Chain another non-generic operation
- `ThenAsync(Func<Task<Result<TResult>>>)` - Chain and produce typed result
- `ThenGetAsync<TResult>(...)` - Query single record
- `ThenGetScalarAsync<TResult>(...)` - Query scalar value
- `ThenInsertAsync(...)` - Insert with optional result selector
- `ThenUpdateAsync(...)` - Update with optional result selector
- `ThenDeleteAsync(...)` - Delete record

### Using Previous Values

Insert, Update, and Delete methods provide overloads that access the previous result value:

```csharp
// Use customer data to build order parameters
.ThenInsertAsync(
    "INSERT INTO Orders (OrderId, CustomerId, Tier) VALUES (@OrderId, @CustomerId, @Tier)",
    customer => new { OrderId = orderId, customer.CustomerId, customer.Tier })
```

### Returning Values from Mutations

Use result selectors to return values from Insert/Update operations:

```csharp
var orderId = Guid.CreateVersion7();

return await conn.ExecuteInTransactionAsync(db =>
    db.InsertAsync(
        "INSERT INTO Orders (...) VALUES (...)",
        new { OrderId = orderId, ... },
        () => orderId)  // Returns the new order ID
    .ThenGetAsync<OrderDto>(
        "SELECT * FROM Orders WHERE OrderId = @Id",
        new { Id = orderId },
        key: orderId)
, ct);
```

### Error Short-Circuiting

Failures propagate without executing subsequent operations:

```csharp
await conn.ExecuteInTransactionAsync(db =>
    db.GetAsync<CustomerDto>(...)          // Returns NotFound
    .ThenInsertAsync(...)                  // Skipped
    .ThenUpdateAsync(...)                  // Skipped
    .ThenGetAsync<OrderDto>(...)           // Skipped - returns original NotFound
, ct);
```

## Configuration

### Programmatic Configuration
```csharp
// Simple connection string
builder.AddDapperSql("default", "Server=localhost;Database=MyDb;Trusted_Connection=true");

// Full configuration
builder.AddDapperSql("default", settings => {
    settings.ConnectionString = "Server=myserver.database.windows.net;Database=MyDb";
    settings.UseAzureAuthentication = true;
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
          "UseAzureAuthentication": true,
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

## Azure Authentication

When `UseAzureAuthentication` is enabled, the connection factory uses `DefaultAzureCredential` to obtain access tokens for Azure SQL Database. This supports:

- Managed Identity (recommended for production)
- Azure CLI credentials (for local development)
- Visual Studio / VS Code credentials
- Environment variables
```csharp
builder.AddDapperSql("default", settings => {
    settings.ConnectionString = "Server=myserver.database.windows.net;Database=MyDb";
    settings.UseAzureAuthentication = true;
});
```

## Contribution Guidelines

1. **Be conservative with new abstractions** � The API surface must remain stable and meaningful.
2. **Limit dependency expansion** � Only add foundational, version-stable dependencies.
3. **Favor additive, non-breaking changes** � Breaking changes ripple through the entire ecosystem.
4. **Include thorough unit tests** � All primitives and patterns should be independently testable.
5. **Document architectural decisions** � Context and reasoning should be clear for future maintainers.
6. **Follow .NET conventions** � Use established patterns from Microsoft.Extensions.* libraries.

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