namespace Cirreum.Persistence.Dapper.Test;

using Cirreum;
using Cirreum.Exceptions;
using Microsoft.Data.Sqlite;
using System.Data;
using static global::Dapper.SqlMapper;

/// <summary>
/// Tests that verify exception handling behavior in database operations.
/// Specifically tests that exceptions in ExecuteTransactionAsync are captured
/// and properly rolled back, converting exceptions to Result failures.
/// </summary>
/// <remarks>
/// Note: The library uses SqlException-specific handling for constraint violations.
/// SQLite throws SqliteException, which is a different type. These tests focus on
/// the generic exception handling path in ExecuteTransactionAsync.
/// </remarks>
[TestClass]
public sealed class ExceptionHandlingTests {

	private static SqliteConnection CreateConnection() {
		var connection = new SqliteConnection("Data Source=:memory:");
		connection.Open();
		return connection;
	}

	private static void CreateTestSchema(IDbConnection connection) {
		connection.Execute("""
			CREATE TABLE Users (
				Id TEXT PRIMARY KEY,
				Name TEXT NOT NULL,
				Email TEXT UNIQUE NOT NULL
			);

			CREATE TABLE Orders (
				Id TEXT PRIMARY KEY,
				UserId TEXT NOT NULL,
				Amount REAL NOT NULL,
				FOREIGN KEY (UserId) REFERENCES Users(Id)
			);
			""");
	}

	#region Transaction Exception Handling - General Exceptions

	[TestMethod]
	public async Task ExecuteTransactionAsync_WhenActionThrowsException_RollsBackAndReturnsFailure() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();

		// Act
		var result = await conn.ExecuteTransactionAsync(async ctx => {
			await ctx.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "John", Email = "john@test.com" });

			throw new ApplicationException("Something went wrong mid-transaction!");
#pragma warning disable CS0162 // Unreachable code detected
			return Result.Success;
#pragma warning restore CS0162
		}, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<ApplicationException>(result.Error);
		Assert.AreEqual("Something went wrong mid-transaction!", result.Error.Message);

		// Verify rollback - user should not exist
		var user = conn.QuerySingleOrDefault<UserDto>(
			"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
			new { Id = userId });
		Assert.IsNull(user);
	}

	[TestMethod]
	public async Task ExecuteTransactionAsync_Generic_WhenActionThrowsException_RollsBackAndReturnsFailure() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();

		// Act
		var result = await conn.ExecuteTransactionAsync<string>(async ctx => {
			await ctx.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "John", Email = "john@test.com" });

			throw new InvalidOperationException("Cannot complete transaction!");
#pragma warning disable CS0162 // Unreachable code detected
			return userId;
#pragma warning restore CS0162
		}, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<InvalidOperationException>(result.Error);
		Assert.AreEqual("Cannot complete transaction!", result.Error.Message);

		// Verify rollback - user should not exist
		var user = conn.QuerySingleOrDefault<UserDto>(
			"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
			new { Id = userId });
		Assert.IsNull(user);
	}

	[TestMethod]
	public async Task ExecuteTransactionAsync_WhenMapperThrowsInChain_RollsBackAndReturnsFailure() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();

		// Act - Insert then map throws
		var result = await conn.ExecuteTransactionAsync<User>(ctx =>
			ctx.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "John", Email = "john@test.com" },
				() => new UserDto(userId, "John", "john@test.com"))
			.MapAsync(_ => {
				static User ThrowingMapper() => throw new FormatException("Bad format!");
				return ThrowingMapper();
			})
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<FormatException>(result.Error);
		Assert.AreEqual("Bad format!", result.Error.Message);

		// Verify rollback - user should not exist
		var user = conn.QuerySingleOrDefault<UserDto>(
			"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
			new { Id = userId });
		Assert.IsNull(user);
	}

	[TestMethod]
	public async Task ExecuteTransactionAsync_WhenWherePredicateThrows_RollsBackAndReturnsFailure() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();

		// Act - Insert then where predicate throws
		var result = await conn.ExecuteTransactionAsync<UserDto>(ctx =>
			ctx.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "John", Email = "john@test.com" },
				() => new UserDto(userId, "John", "john@test.com"))
			.WhereAsync(
				_ => {
					static bool ThrowingPredicate() => throw new ArithmeticException("Cannot evaluate predicate!");
					return ThrowingPredicate();
				},
				new BadRequestException("Validation failed"))
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<ArithmeticException>(result.Error);
		Assert.AreEqual("Cannot evaluate predicate!", result.Error.Message);

		// Verify rollback - user should not exist
		var user = conn.QuerySingleOrDefault<UserDto>(
			"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
			new { Id = userId });
		Assert.IsNull(user);
	}

	[TestMethod]
	public async Task ExecuteTransactionAsync_WhenParametersFactoryThrows_RollsBackAndReturnsFailure() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		var orderId = Guid.NewGuid().ToString();

		// Act - Insert user, then parameters factory for order throws
		var result = await conn.ExecuteTransactionAsync(ctx =>
			ctx.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "John", Email = "john@test.com" },
				() => userId)
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				_ => throw new ArgumentNullException("userId", "Parameters factory failed!"))
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<ArgumentNullException>(result.Error);

		// Verify rollback - user should not exist
		var user = conn.QuerySingleOrDefault<UserDto>(
			"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
			new { Id = userId });
		Assert.IsNull(user);
	}

	[TestMethod]
	public async Task ExecuteTransactionAsync_WhenResultSelectorThrows_RollsBackAndReturnsFailure() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();

		// Act - Insert with result selector that throws
		var result = await conn.ExecuteTransactionAsync<string>(ctx =>
			ctx.InsertAsync<string>(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "John", Email = "john@test.com" },
				() => throw new InvalidCastException("Result selector failed!"))
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<InvalidCastException>(result.Error);
		Assert.AreEqual("Result selector failed!", result.Error.Message);

		// Verify rollback - user should not exist
		var user = conn.QuerySingleOrDefault<UserDto>(
			"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
			new { Id = userId });
		Assert.IsNull(user);
	}

	#endregion

	#region Transaction SQL Error Handling

	[TestMethod]
	public async Task ExecuteTransactionAsync_WhenSqlSyntaxError_RollsBackAndReturnsFailure() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();

		// Act - First insert succeeds, second has syntax error
		var result = await conn.ExecuteTransactionAsync(async ctx => {
			await ctx.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "John", Email = "john@test.com" });

			// Syntax error in second insert
			return await ctx.InsertAsync(
				"INSRT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)", // INSRT instead of INSERT
				new { Id = Guid.NewGuid().ToString(), UserId = userId, Amount = 100.0 });
		}, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<SqliteException>(result.Error);

		// Verify rollback - first user insert should be rolled back too
		var user = conn.QuerySingleOrDefault<UserDto>(
			"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
			new { Id = userId });
		Assert.IsNull(user);
	}

	[TestMethod]
	public async Task ExecuteTransactionAsync_WhenConstraintViolation_RollsBackAndReturnsFailure() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId1 = Guid.NewGuid().ToString();
		var userId2 = Guid.NewGuid().ToString();

		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId1, Name = "Existing", Email = "existing@test.com" });

		// Act - Insert user, then try to update to conflict with existing email
		var result = await conn.ExecuteTransactionAsync(ctx =>
			ctx.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId2, Name = "New", Email = "new@test.com" })
			.ThenUpdateAsync(
				"UPDATE Users SET Email = @Email WHERE Id = @Id",
				new { Id = userId2, Email = "existing@test.com" }, // Conflicts with existing user
				userId2)
		, this.TestContext.CancellationToken);

		// Assert - SqliteException is caught by the general Exception handler
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<SqliteException>(result.Error);

		// Verify rollback - new user should not exist
		var user = conn.QuerySingleOrDefault<UserDto>(
			"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
			new { Id = userId2 });
		Assert.IsNull(user);
	}

	#endregion

	#region Chained Operations Exception Propagation

	[TestMethod]
	public async Task FluentChain_WhenMiddleOperationThrows_PropagatesFailureAndStopsChain() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		var finalOperationExecuted = false;

		// Act
		var result = await conn.ExecuteTransactionAsync<long>(ctx =>
			ctx.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "John", Email = "john@test.com" },
				() => new UserDto(userId, "John", "john@test.com"))
			.MapAsync(_ => {
				static User ThrowingMapper() => throw new NotImplementedException("Middle mapper failed!");
				return ThrowingMapper();
			})
			.ThenGetScalarAsync<long>("SELECT COUNT(*) FROM Users")
			.MapAsync(count => {
				finalOperationExecuted = true;
				return count;
			})
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<NotImplementedException>(result.Error);
		Assert.AreEqual("Middle mapper failed!", result.Error.Message);
		Assert.IsFalse(finalOperationExecuted, "Operations after failure should not execute");
	}

	[TestMethod]
	public async Task FluentChain_ExceptionInFirstOperation_PropagatesAndRollsBack() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var orderId = Guid.NewGuid().ToString();
		var orderInsertExecuted = false;

		// Act - First operation's result selector throws, second should not run
		var result = await conn.ExecuteTransactionAsync(ctx =>
			ctx.InsertAsync<UserDto>(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = Guid.NewGuid().ToString(), Name = "John", Email = "john@test.com" },
				() => throw new AccessViolationException("First operation failed!"))
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				user => {
					orderInsertExecuted = true;
					return new { Id = orderId, UserId = user.Id, Amount = 100.0 };
				})
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<AccessViolationException>(result.Error);
		Assert.IsFalse(orderInsertExecuted, "Second operation should not execute after first fails");
	}

	[TestMethod]
	public async Task FluentChain_MultipleInsertsWithExceptionInMiddle_RollsBackAll() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		var orderId1 = Guid.NewGuid().ToString();
		var orderId2 = Guid.NewGuid().ToString();

		// Act - Insert user, insert order 1, throw, insert order 2 (should never run)
		var result = await conn.ExecuteTransactionAsync(ctx =>
			ctx.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "John", Email = "john@test.com" },
				() => userId)
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				_ => new { Id = orderId1, UserId = userId, Amount = 100.0 },
				() => orderId1)
			.MapAsync(_ => {
				static string Throw() => throw new Exception("Boom!");
				return Throw();
			})
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				_ => new { Id = orderId2, UserId = userId, Amount = 200.0 })
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsFailure);

		// Verify ALL operations were rolled back
		var user = conn.QuerySingleOrDefault<UserDto>(
			"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
			new { Id = userId });
		Assert.IsNull(user, "User should be rolled back");

		var order1 = conn.QuerySingleOrDefault<OrderDto>(
			"SELECT Id, UserId, Amount FROM Orders WHERE Id = @Id",
			new { Id = orderId1 });
		Assert.IsNull(order1, "Order 1 should be rolled back");
	}

	#endregion

	#region Test DTOs

	private record UserDto(string Id, string Name, string Email);
	private record User(string Id, string Name, string Email);
	private record OrderDto(string Id, string UserId, double Amount);

	public TestContext TestContext { get; set; }

	#endregion

}
