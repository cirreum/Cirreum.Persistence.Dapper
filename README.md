# Cirreum.Persistence.Dapper

[![NuGet Version](https://img.shields.io/nuget/v/Cirreum.Persistence.Dapper.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Persistence.Dapper/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cirreum.Persistence.Dapper.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Persistence.Dapper/)
[![GitHub Release](https://img.shields.io/github/v/release/cirreum/Cirreum.Persistence.Dapper?style=flat-square&labelColor=1F1F1F&color=FF3B2E)](https://github.com/cirreum/Cirreum.Persistence.Dapper/releases)
[![License](https://img.shields.io/github/license/cirreum/Cirreum.Persistence.Dapper?style=flat-square&labelColor=1F1F1F&color=F2F2F2)](https://github.com/cirreum/Cirreum.Persistence.Dapper/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-003D8F?style=flat-square&labelColor=1F1F1F)](https://dotnet.microsoft.com/)

**Lightweight SQL Server persistence layer using Dapper for .NET applications**

## Overview

**Cirreum.Persistence.Dapper** provides a streamlined SQL Server database connection factory using Dapper. Built to integrate seamlessly with the Cirreum Foundation Framework, it offers flexible authentication options including Azure Entra ID support for modern cloud-native applications.

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

For "preview with expand" scenarios - load an initial batch and indicate if more exist. Not for pagination.
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

The library provides two result wrapper types that enable fluent chaining within transactions:
- **`DbResult`** - Non-generic result for operations that don't return a value
- **`DbResult<T>`** - Generic result that carries a value through the chain

These types wrap `Result`/`Result<T>` along with the `TransactionContext`, enabling method chaining while preserving transaction scope. They function similarly to a Reader monad, threading the transaction context through each operation.

### Basic Chaining
```csharp
public async Task<Result<OrderDto>> CreateOrderWithItemsAsync(
    CreateOrder command, CancellationToken ct)
{
    await using var conn = await db.CreateConnectionAsync(ct);

    return await conn.ExecuteTransactionAsync(ctx =>
        ctx.GetAsync<CustomerDto>(
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
- `EnsureAsync(Func<T, bool>, Exception)` - Validate with predicate
- `ThenAsync(Func<T, Task<Result>>)` - Escape hatch for external async operations
- `ThenAsync<TResult>(Func<T, Task<Result<TResult>>>)` - Escape hatch that transforms to new type
- `ThenGetAsync<TResult>(...)` - Query single record
- `ThenGetScalarAsync<TResult>(...)` - Query scalar value
- `ThenQueryAnyAsync<TResult>(...)` - Query collection
- `ThenInsertAsync(...)` - Insert, returns `DbResult<T>` (pass-through)
- `ThenInsertAndReturnAsync<TResult>(...)` - Insert, returns `DbResult<TResult>` (transform)
- `ThenUpdateAsync(...)` - Update, returns `DbResult<T>` (pass-through)
- `ThenUpdateAndReturnAsync<TResult>(...)` - Update, returns `DbResult<TResult>` (transform)
- `ThenDeleteAsync(...)` - Delete, returns `DbResult<T>` (pass-through)
- `ThenDeleteAndReturnAsync<TResult>(...)` - Delete, returns `DbResult<TResult>` (transform)
- `ThenInsertIfAsync(..., when)` - Conditional insert, pass-through (see below)
- `ThenInsertIfAndReturnAsync<TResult>(..., when)` - Conditional insert with transform (see below)
- `ThenUpdateIfAsync(..., when)` - Conditional update, pass-through (see below)
- `ThenUpdateIfAndReturnAsync<TResult>(..., when)` - Conditional update with transform (see below)
- `ThenDeleteIfAsync(..., when)` - Conditional delete, pass-through (see below)
- `ToResult()` - Convert `DbResult<T>` to `DbResult` (discard value)

**From `DbResult` (void result):**
- `ThenAsync(Func<Task<Result>>)` - Escape hatch for external async operations
- `ThenAsync<T>(Func<Task<Result<T>>>)` - Escape hatch that produces typed result
- `ThenGetAsync<TResult>(...)` - Query single record
- `ThenGetScalarAsync<TResult>(...)` - Query scalar value
- `ThenQueryAnyAsync<TResult>(...)` - Query collection
- `ThenInsertAsync(...)` - Insert, returns `DbResult`
- `ThenInsertAndReturnAsync<T>(..., resultSelector)` - Insert, returns `DbResult<T>`
- `ThenUpdateAsync(...)` - Update, returns `DbResult`
- `ThenUpdateAndReturnAsync<T>(..., resultSelector)` - Update, returns `DbResult<T>`
- `ThenDeleteAsync(...)` - Delete, returns `DbResult`
- `ThenInsertIfAsync(..., when)` - Conditional insert
- `ThenInsertIfAndReturnAsync<T>(..., resultSelector, when)` - Conditional insert with transform
- `ThenUpdateIfAsync(..., when)` - Conditional update
- `ThenUpdateIfAndReturnAsync<T>(..., resultSelector, when)` - Conditional update with transform
- `ThenDeleteIfAsync(..., when)` - Conditional delete

### Using Previous Values

Insert, Update, and Delete methods provide overloads that access the previous result value:

```csharp
// Use customer data to build order parameters
.ThenInsertAsync(
    "INSERT INTO Orders (OrderId, CustomerId, Tier) VALUES (@OrderId, @CustomerId, @Tier)",
    customer => new { OrderId = orderId, customer.CustomerId, customer.Tier })
```

### Returning Values from Mutations

Use result selectors to return values from Insert/Update operations. The `AndReturn` variants transform the result type:

```csharp
var orderId = Guid.CreateVersion7();

return await conn.ExecuteTransactionAsync(ctx =>
    ctx.InsertAndReturnAsync(
        "INSERT INTO Orders (...) VALUES (...)",
        new { OrderId = orderId, ... },
        () => orderId)  // Returns the new order ID as DbResult<Guid>
    .ThenGetAsync<OrderDto>(
        "SELECT * FROM Orders WHERE OrderId = @Id",
        new { Id = orderId },
        key: orderId)
, ct);
```

From `DbResult<T>`, use `ThenInsertAndReturnAsync` to transform to a new type:

```csharp
return await conn.ExecuteTransactionAsync(ctx =>
    ctx.GetAsync<CustomerDto>(
        "SELECT * FROM Customers WHERE CustomerId = @Id",
        new { Id = customerId },
        customerId)
    .ThenInsertAndReturnAsync(
        "INSERT INTO Orders (...) VALUES (...)",
        c => new { OrderId = orderId, c.CustomerId, ... },
        c => orderId)  // Transforms CustomerDto -> Guid
    .ThenGetAsync<OrderDto>(
        "SELECT * FROM Orders WHERE OrderId = @Id",
        new { Id = orderId },
        orderId)
, ct);
```

### Conditional Operations

The `ThenInsertIfAsync`, `ThenUpdateIfAsync`, and `ThenDeleteIfAsync` methods allow conditional execution. If the `when` predicate returns `false`, the operation is skipped and the chain continues.

**From `DbResult<T>`** - The predicate receives the current value:

```csharp
return await conn.ExecuteTransactionAsync(ctx =>
    ctx.GetAsync<CustomerDto>(
        "SELECT * FROM Customers WHERE CustomerId = @Id",
        new { Id = customerId },
        customerId)
    .ThenInsertIfAsync(
        "INSERT INTO AuditLog (CustomerId, Action) VALUES (@CustomerId, @Action)",
        c => new { c.CustomerId, Action = "Accessed" },
        when: c => c.TrackActivity)  // Only insert if tracking enabled; CustomerDto passes through
    .ThenUpdateIfAsync(
        "UPDATE Customers SET LastAccessedAt = @Now WHERE CustomerId = @CustomerId",
        c => new { c.CustomerId, Now = DateTime.UtcNow },
        customerId,
        when: c => c.IsActive)  // Only update if active; CustomerDto passes through
, ct);
```

**From `DbResult`** - The predicate is a simple `Func<bool>`:

```csharp
var request = new { ShouldAudit = true };

return await conn.ExecuteTransactionAsync(ctx =>
    ctx.InsertAsync(
        "INSERT INTO Orders (...) VALUES (...)",
        new { OrderId = orderId, ... })
    .ThenInsertIfAsync(
        "INSERT INTO AuditLog (...) VALUES (...)",
        new { ... },
        when: () => request.ShouldAudit)  // Captures external value
, ct);
```

### Conditional Operations with Type Transformation

Use `ThenInsertIfAndReturnAsync`, `ThenUpdateIfAndReturnAsync`, etc. when you need the type to transform regardless of whether the operation executes:

**From `DbResult<T>` to `DbResult<TResult>`:**

```csharp
return await conn.ExecuteTransactionAsync(ctx =>
    ctx.GetAsync<CustomerDto>(
        "SELECT * FROM Customers WHERE CustomerId = @Id",
        new { Id = customerId },
        customerId)
    .ThenInsertIfAndReturnAsync(
        "INSERT INTO Orders (...) VALUES (...)",
        c => new { OrderId = orderId, c.CustomerId, ... },
        c => orderId,  // resultSelector: CustomerDto -> string (orderId)
        when: c => c.IsActive)
    .ThenUpdateAsync(  // Now receives string (orderId), not CustomerDto
        "UPDATE Orders SET Status = @Status WHERE OrderId = @Id",
        oid => new { Id = oid, Status = "Confirmed" },
        orderId)
    .ToResult()  // Convert to DbResult when value is no longer needed
, ct);
```

**From `DbResult` to `DbResult<T>`:**

```csharp
return await conn.ExecuteTransactionAsync<string>(ctx =>
    ctx.InsertAsync(
        "INSERT INTO Users (...) VALUES (...)",
        new { ... })
    .ThenInsertIfAsync(
        "INSERT INTO Orders (...) VALUES (...)",
        new { OrderId = orderId, ... },
        () => orderId,  // resultSelector: transforms DbResult -> DbResult<string>
        when: () => shouldCreateOrder)
    .ThenUpdateAsync(  // Receives string (orderId)
        "UPDATE Orders SET Amount = @Amount WHERE Id = @Id",
        oid => new { Id = oid, Amount = 100.0 },
        orderId)
, ct);
```

The key insight: **`resultSelector` always runs** (when the chain is successful), even if `when` returns `false` and the operation is skipped. This allows consistent type transformation for subsequent operations.

**Pass-through pattern:** If you want the original type to pass through (no transformation), use the non-`AndReturn` variants:

```csharp
return await conn.ExecuteTransactionAsync(ctx =>
    ctx.GetAsync<CustomerDto>(...)
    .ThenInsertIfAsync(
        "INSERT INTO PremiumCustomers (...) VALUES (...)",
        c => new { c.CustomerId, ... },
        when: c => c.IsPremium)  // CustomerDto passes through
    .MapAsync(c => new CustomerSummary(c.CustomerId, c.Name))  // Transform after
, ct);
```

### Escape Hatch: ThenAsync

The `ThenAsync` methods allow you to integrate external async operations that return `Result` types into the fluent chain. This is useful for calling external services, complex validation, or chaining to other repositories:

```csharp
return await conn.ExecuteTransactionAsync(ctx =>
    ctx.GetAsync<CustomerDto>(
        "SELECT * FROM Customers WHERE CustomerId = @Id",
        new { Id = customerId },
        customerId)
    .ThenAsync(async customer => {
        // Call external service - if it fails, transaction rolls back
        return await paymentService.ValidateCustomerAsync(customer.CustomerId);
    })
    .ThenInsertAsync(
        "INSERT INTO Orders (...) VALUES (...)",
        new { OrderId = orderId, CustomerId = customerId, ... })
, ct);
```

Use `ThenAsync<TResult>` when the external operation produces a value needed by subsequent operations:

```csharp
return await conn.ExecuteTransactionAsync(ctx =>
    ctx.GetAsync<CustomerDto>(...)
    .ThenAsync<PaymentToken>(async customer => {
        // External call returns a value for the chain
        return await paymentService.CreateTokenAsync(customer.CustomerId);
    })
    .ThenInsertAsync(
        "INSERT INTO Orders (...) VALUES (...)",
        token => new { OrderId = orderId, PaymentToken = token.Value, ... })
, ct);
```

### Error Short-Circuiting

Failures propagate without executing subsequent operations:

```csharp
await conn.ExecuteTransactionAsync(ctx =>
    ctx.GetAsync<CustomerDto>(...)         // Returns NotFound
    .ThenInsertAsync(...)                  // Skipped
    .ThenUpdateAsync(...)                  // Skipped
    .ThenGetAsync<OrderDto>(...)           // Skipped - returns original NotFound
, ct);
```

## Factory Extensions

For simple operations, `IDbConnectionFactory` provides extension methods that handle connection management automatically, reducing boilerplate:

```csharp
// Instead of this...
await using var conn = await db.CreateConnectionAsync(ct);
return await conn.GetAsync<OrderDto>(sql, parameters, key, ct);

// You can write this...
return await db.GetAsync<OrderDto>(sql, parameters, key, ct);
```

### Available Factory Extensions

| Method | Description |
|--------|-------------|
| `ExecuteAsync(action)` | Execute custom action with managed connection |
| `ExecuteTransactionAsync(action)` | Execute transaction with managed connection |
| `GetAsync<T>(...)` | Query single record |
| `GetScalarAsync<T>(...)` | Query scalar value |
| `QueryAnyAsync<T>(...)` | Query collection |
| `InsertAsync(...)` | Insert record |
| `InsertAndReturnAsync<T>(...)` | Insert record and return value via resultSelector |
| `UpdateAsync(...)` | Update record |
| `UpdateAndReturnAsync<T>(...)` | Update record and return value via resultSelector |
| `DeleteAsync(...)` | Delete record |

All factory extensions capture exceptions and convert them to `Result` failures, ensuring consistent error handling.

### Example Usage

```csharp
public class OrderRepository(IDbConnectionFactory db)
{
    public Task<Result<OrderDto>> GetOrderAsync(Guid orderId, CancellationToken ct)
        => db.GetAsync<OrderDto>(
            "SELECT * FROM Orders WHERE OrderId = @Id",
            new { Id = orderId },
            orderId,
            ct);

    public Task<Result<Guid>> CreateOrderAsync(CreateOrder cmd, CancellationToken ct)
    {
        var orderId = Guid.CreateVersion7();
        return db.InsertAndReturnAsync(
            "INSERT INTO Orders (OrderId, CustomerId, Amount) VALUES (@OrderId, @CustomerId, @Amount)",
            new { OrderId = orderId, cmd.CustomerId, cmd.Amount },
            () => orderId,
            ct);
    }

    public Task<Result<OrderDto>> CreateOrderWithValidationAsync(CreateOrder cmd, CancellationToken ct)
        => db.ExecuteTransactionAsync(ctx =>
            ctx.GetAsync<CustomerDto>(
                "SELECT * FROM Customers WHERE CustomerId = @Id",
                new { Id = cmd.CustomerId },
                cmd.CustomerId)
            .EnsureAsync(
                c => c.IsActive,
                new BadRequestException("Customer is not active"))
            .ThenInsertAndReturnAsync(
                "INSERT INTO Orders (...) VALUES (...)",
                c => new { OrderId = Guid.CreateVersion7(), c.CustomerId, cmd.Amount },
                c => new OrderDto(...))  // Transform CustomerDto -> OrderDto
        , ct);
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