namespace Cirreum.Persistence.Dapper.Test;

using Cirreum;
using Cirreum.Exceptions;
using Microsoft.Data.Sqlite;
using System.Data;
using static global::Dapper.SqlMapper;

[TestClass]
public sealed class SqliteIntegrationTests {

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

	#region Query Methods

	[TestMethod]
	public async Task GetAsync_WhenRecordExists_ReturnsSuccess() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		// Act
		var result = await conn.GetAsync<UserDto>(
			"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
			new { Id = userId },
			userId, cancellationToken: this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual("John", result.Value.Name);
		Assert.AreEqual("john@test.com", result.Value.Email);
	}

	[TestMethod]
	public async Task GetAsync_WhenRecordDoesNotExist_ReturnsFailure() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);

		// Act
		var result = await conn.GetAsync<UserDto>(
			"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
			new { Id = "nonexistent" },
			"nonexistent", cancellationToken: this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<NotFoundException>(result.Error);
	}

	[TestMethod]
	public async Task GetAsync_WithMapper_TransformsResult() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "Jane", Email = "jane@test.com" });

		// Act
		var result = await conn.GetAsync<UserDto, User>(
			"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
			new { Id = userId },
			userId,
			dto => new User(dto.Id, dto.Name, dto.Email), cancellationToken: this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual("Jane", result.Value.Name);
	}

	[TestMethod]
	public async Task QueryAnyAsync_ReturnsAllRecords() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = Guid.NewGuid().ToString(), Name = "User1", Email = "user1@test.com" });
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = Guid.NewGuid().ToString(), Name = "User2", Email = "user2@test.com" });

		// Act
		var result = await conn.QueryAnyAsync<UserDto>("SELECT Id, Name, Email FROM Users", cancellationToken: this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.HasCount(2, result.Value);
	}

	[TestMethod]
	public async Task QueryAnyAsync_WhenEmpty_ReturnsEmptyList() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);

		// Act
		var result = await conn.QueryAnyAsync<UserDto>("SELECT Id, Name, Email FROM Users", cancellationToken: this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.IsEmpty(result.Value);
	}

	[TestMethod]
	public async Task GetScalarAsync_ReturnsScalarValue() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = Guid.NewGuid().ToString(), Name = "User1", Email = "user1@test.com" });
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = Guid.NewGuid().ToString(), Name = "User2", Email = "user2@test.com" });

		// Act
		var result = await conn.GetScalarAsync<long>("SELECT COUNT(*) FROM Users", cancellationToken: this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual(2L, result.Value);
	}

	[TestMethod]
	public async Task GetScalarAsync_WithMapper_TransformsResult() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = Guid.NewGuid().ToString(), Name = "User1", Email = "user1@test.com" });

		// Act
		var result = await conn.GetScalarAsync<long, string>(
			"SELECT COUNT(*) FROM Users",
			count => $"Total: {count}", cancellationToken: this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual("Total: 1", result.Value);
	}

	#endregion

	#region TransactionBuilder Basic Operations

	[TestMethod]
	public async Task TransactionBuilder_GetAsync_ReturnsDbResult() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "Test", Email = "test@test.com" });

		// Act
		var result = await conn.ExecuteTransactionAsync<UserDto>(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual("Test", result.Value.Name);
	}

	[TestMethod]
	public async Task TransactionBuilder_GetScalarAsync_ReturnsValue() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = Guid.NewGuid().ToString(), Name = "Test", Email = "test@test.com" });

		// Act
		var result = await conn.ExecuteTransactionAsync(async db => {
			return await db.GetScalarAsync<long>("SELECT COUNT(*) FROM Users");
		}, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual(1L, result.Value);
	}

	[TestMethod]
	public async Task TransactionBuilder_QueryAnyAsync_ReturnsResults() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = Guid.NewGuid().ToString(), Name = "User1", Email = "user1@test.com" });
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = Guid.NewGuid().ToString(), Name = "User2", Email = "user2@test.com" });

		// Act
		var result = await conn.ExecuteTransactionAsync(async db => {
			return await db.QueryAnyAsync<UserDto>("SELECT Id, Name, Email FROM Users");
		}, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.HasCount(2, result.Value);
	}

	#endregion

	#region Fluent Chaining

	[TestMethod]
	public async Task FluentChaining_GetThenMap_Success() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		// Act
		var result = await conn.ExecuteTransactionAsync<User>(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.MapAsync(u => new User(u.Id, u.Name, u.Email))
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual("John", result.Value.Name);
	}

	[TestMethod]
	public async Task FluentChaining_GetThenWhere_PassesFilter() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "ValidUser", Email = "valid@test.com" });

		// Act
		var result = await conn.ExecuteTransactionAsync<UserDto>(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.EnsureAsync(u => u.Name.StartsWith("Valid"), new BadRequestException("Invalid user"))
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual("ValidUser", result.Value.Name);
	}

	[TestMethod]
	public async Task FluentChaining_GetThenEnsure_FailsValidation() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "InvalidUser", Email = "invalid@test.com" });
		var expectedError = new BadRequestException("User must start with 'Valid'");

		// Act
		var result = await conn.ExecuteTransactionAsync<UserDto>(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.EnsureAsync(u => u.Name.StartsWith("Valid"), expectedError)
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.AreSame(expectedError, result.Error);
	}

	[TestMethod]
	public async Task FluentChaining_GetThenGetScalar_ChainsOperations() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });
		conn.Execute("INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
			new { Id = Guid.NewGuid().ToString(), UserId = userId, Amount = 100.0 });
		conn.Execute("INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
			new { Id = Guid.NewGuid().ToString(), UserId = userId, Amount = 200.0 });

		// Act
		var result = await conn.ExecuteTransactionAsync<long>(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenGetScalarAsync<long>(
				"SELECT COUNT(*) FROM Orders WHERE UserId = @UserId",
				new { UserId = userId })
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual(2L, result.Value);
	}

	[TestMethod]
	public async Task FluentChaining_GetThenQueryAny_ChainsOperations() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });
		conn.Execute("INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
			new { Id = Guid.NewGuid().ToString(), UserId = userId, Amount = 100.0 });
		conn.Execute("INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
			new { Id = Guid.NewGuid().ToString(), UserId = userId, Amount = 200.0 });

		// Act
		var result = await conn.ExecuteTransactionAsync<IReadOnlyList<OrderDto>>(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenQueryAnyAsync<OrderDto>(
				"SELECT Id, UserId, Amount FROM Orders WHERE UserId = @UserId",
				new { UserId = userId })
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.HasCount(2, result.Value);
		Assert.AreEqual(100.0, result.Value[0].Amount);
		Assert.AreEqual(200.0, result.Value[1].Amount);
	}

	[TestMethod]
	public async Task FluentChaining_NotFoundPropagates_ThroughChain() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var scalarExecuted = false;

		// Act
		var result = await conn.ExecuteTransactionAsync<long>(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = "nonexistent" },
				"nonexistent")
			.ThenGetScalarAsync<long>("SELECT COUNT(*) FROM Orders")
			.MapAsync(count => {
				scalarExecuted = true;
				return count;
			})
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<NotFoundException>(result.Error);
		Assert.IsFalse(scalarExecuted, "Subsequent operations should not execute after failure");
	}

	[TestMethod]
	public async Task FluentChaining_MultipleGetOperations() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var user1Id = Guid.NewGuid().ToString();
		var user2Id = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = user1Id, Name = "User1", Email = "user1@test.com" });
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = user2Id, Name = "User2", Email = "user2@test.com" });

		// Act
		var result = await conn.ExecuteTransactionAsync<UserDto>(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = user1Id },
				user1Id)
			.ThenGetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = user2Id },
				user2Id)
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual("User2", result.Value.Name); // Second get result
	}

	#endregion

	#region Insert/Update/Delete Chaining

	[TestMethod]
	public async Task FluentChaining_GetThenInsert_ReturnsDbResultNonGeneric() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		var orderId = Guid.NewGuid().ToString();

		// Act
		var result = await conn.ExecuteTransactionAsync(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				new { Id = orderId, UserId = userId, Amount = 150.0 })
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var count = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Orders WHERE Id = @Id", new { Id = orderId });
		Assert.AreEqual(1L, count);
	}

	[TestMethod]
	public async Task FluentChaining_GetThenInsertWithParametersFactory_UsesPreviousValue() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		var orderId = Guid.NewGuid().ToString();

		// Act - use the fetched user's ID in the insert
		var result = await conn.ExecuteTransactionAsync(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				user => new { Id = orderId, UserId = user.Id, Amount = 250.0 })
			, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var order = conn.QueryFirstOrDefault<OrderDto>(
			"SELECT Id, UserId, Amount FROM Orders WHERE Id = @Id",
			new { Id = orderId });
		Assert.IsNotNull(order);
		Assert.AreEqual(userId, order.UserId);
		Assert.AreEqual(250.0, order.Amount);
	}

	[TestMethod]
	public async Task FluentChaining_GetThenInsertWithResultSelector_ReturnsDbResultT() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		var orderId = Guid.NewGuid().ToString();

		// Act
		var result = await conn.ExecuteTransactionAsync<string>(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				new { Id = orderId, UserId = userId, Amount = 350.0 },
				() => orderId)
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual(orderId, result.Value);
	}

	[TestMethod]
	public async Task FluentChaining_GetThenInsertWithFactoryAndResultSelector_ReturnsDbResultT() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		var orderId = Guid.NewGuid().ToString();

		// Act
		var result = await conn.ExecuteTransactionAsync<(string OrderId, string UserName)>(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				user => new { Id = orderId, UserId = user.Id, Amount = 450.0 },
				() => (orderId, "John"))
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual(orderId, result.Value.OrderId);
		Assert.AreEqual("John", result.Value.UserName);
	}

	[TestMethod]
	public async Task FluentChaining_InsertThenGetScalar_ChainsFromNonGeneric() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		var orderId = Guid.NewGuid().ToString();

		// Act - Insert returns DbResultNonGeneric, then chain to ThenGetScalarAsync
		var result = await conn.ExecuteTransactionAsync<long>(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				user => new { Id = orderId, UserId = user.Id, Amount = 100.0 })
			.ThenGetScalarAsync<long>("SELECT COUNT(*) FROM Orders WHERE UserId = @UserId", new { UserId = userId })
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual(1L, result.Value);
	}

	[TestMethod]
	public async Task FluentChaining_InsertThenInsert_ChainsMultipleInserts() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		var orderId1 = Guid.NewGuid().ToString();
		var orderId2 = Guid.NewGuid().ToString();

		// Act
		var result = await conn.ExecuteTransactionAsync(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				user => new { Id = orderId1, UserId = user.Id, Amount = 100.0 })
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				new { Id = orderId2, UserId = userId, Amount = 200.0 })
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var count = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Orders WHERE UserId = @UserId", new { UserId = userId });
		Assert.AreEqual(2L, count);
	}

	[TestMethod]
	public async Task FluentChaining_GetThenUpdate_ReturnsDbResultNonGeneric() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		// Act
		var result = await conn.ExecuteTransactionAsync(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenUpdateAsync(
				"UPDATE Users SET Name = @Name WHERE Id = @Id",
				new { Id = userId, Name = "Jane" },
				userId)
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var updatedName = conn.ExecuteScalar<string>("SELECT Name FROM Users WHERE Id = @Id", new { Id = userId });
		Assert.AreEqual("Jane", updatedName);
	}

	[TestMethod]
	public async Task FluentChaining_GetThenUpdateWithParametersFactory_UsesPreviousValue() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		// Act - update based on fetched user
		var result = await conn.ExecuteTransactionAsync(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenUpdateAsync(
				"UPDATE Users SET Name = @NewName WHERE Id = @Id",
				user => new { user.Id, NewName = user.Name + " Updated" },
				userId)
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var updatedName = conn.ExecuteScalar<string>("SELECT Name FROM Users WHERE Id = @Id", new { Id = userId });
		Assert.AreEqual("John Updated", updatedName);
	}

	[TestMethod]
	public async Task FluentChaining_GetThenUpdateWithResultSelector_ReturnsDbResultT() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		// Act
		var result = await conn.ExecuteTransactionAsync<string>(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenUpdateAsync(
				"UPDATE Users SET Name = @Name WHERE Id = @Id",
				new { Id = userId, Name = "Jane" },
				userId,
				() => "Updated successfully")
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual("Updated successfully", result.Value);
	}

	[TestMethod]
	public async Task FluentChaining_GetThenDelete_ReturnsDbResultNonGeneric() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		// Act
		var result = await conn.ExecuteTransactionAsync(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenDeleteAsync(
				"DELETE FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var count = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Users WHERE Id = @Id", new { Id = userId });
		Assert.AreEqual(0L, count);
	}

	[TestMethod]
	public async Task FluentChaining_GetThenDeleteWithParametersFactory_UsesPreviousValue() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		// Act
		var result = await conn.ExecuteTransactionAsync(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenDeleteAsync(
				"DELETE FROM Users WHERE Id = @Id",
				user => new { user.Id },
				userId)
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var count = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Users WHERE Id = @Id", new { Id = userId });
		Assert.AreEqual(0L, count);
	}

	[TestMethod]
	public async Task FluentChaining_FailurePropagates_ThroughInsertChain() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var insertExecuted = false;

		// Act
		var result = await conn.ExecuteTransactionAsync(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = "nonexistent" },
				"nonexistent")
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				_ => {
					insertExecuted = true;
					return new { Id = Guid.NewGuid().ToString(), UserId = "test", Amount = 100.0 };
				})
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<NotFoundException>(result.Error);
		Assert.IsFalse(insertExecuted, "Insert should not execute after failure");
	}

	[TestMethod]
	public async Task FluentChaining_ComplexChain_GetInsertUpdateGetScalar() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		var orderId = Guid.NewGuid().ToString();

		// Act - Complex chain: Get user -> Insert order -> Update user -> Get count
		var result = await conn.ExecuteTransactionAsync<long>(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				user => new { Id = orderId, UserId = user.Id, Amount = 500.0 })
			.ThenUpdateAsync(
				"UPDATE Users SET Name = @Name WHERE Id = @Id",
				new { Id = userId, Name = "John (with order)" },
				userId)
			.ThenGetScalarAsync<long>("SELECT COUNT(*) FROM Orders WHERE UserId = @UserId", new { UserId = userId })
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual(1L, result.Value);
		var updatedName = conn.ExecuteScalar<string>("SELECT Name FROM Users WHERE Id = @Id", new { Id = userId });
		Assert.AreEqual("John (with order)", updatedName);
	}

	[TestMethod]
	public async Task FluentChaining_DbResultNonGeneric_ThenInsertWithResultSelector() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		var orderId1 = Guid.NewGuid().ToString();
		var orderId2 = Guid.NewGuid().ToString();

		// Act - Insert returning non-generic, then insert returning generic
		var result = await conn.ExecuteTransactionAsync<string>(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				new { Id = orderId1, UserId = userId, Amount = 100.0 })
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				new { Id = orderId2, UserId = userId, Amount = 200.0 },
				() => orderId2)
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual(orderId2, result.Value);
	}

	#endregion

	#region TransactionBuilder Direct Insert/Update/Delete Chaining

	[TestMethod]
	public async Task TransactionBuilder_InsertThenUpdate_ChainsFromInsert() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();

		// Act - Start with InsertAsync directly on TransactionBuilder, then chain UpdateAsync
		var result = await conn.ExecuteTransactionAsync(db =>
			db.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "John", Email = "john@test.com" })
			.ThenUpdateAsync(
				"UPDATE Users SET Name = @Name WHERE Id = @Id",
				new { Id = userId, Name = "Jane" },
				userId)
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var name = conn.ExecuteScalar<string>("SELECT Name FROM Users WHERE Id = @Id", new { Id = userId });
		Assert.AreEqual("Jane", name);
	}

	[TestMethod]
	public async Task TransactionBuilder_InsertThenInsert_ChainsMultipleInserts() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		var orderId = Guid.NewGuid().ToString();

		// Act
		var result = await conn.ExecuteTransactionAsync(db =>
			db.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "John", Email = "john@test.com" })
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				new { Id = orderId, UserId = userId, Amount = 100.0 })
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var userCount = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Users WHERE Id = @Id", new { Id = userId });
		var orderCount = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Orders WHERE Id = @Id", new { Id = orderId });
		Assert.AreEqual(1L, userCount);
		Assert.AreEqual(1L, orderCount);
	}

	[TestMethod]
	public async Task TransactionBuilder_InsertThenGetAsync_ChainsToQuery() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();

		// Act
		var result = await conn.ExecuteTransactionAsync<UserDto>(db =>
			db.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "John", Email = "john@test.com" })
			.ThenGetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual("John", result.Value.Name);
		Assert.AreEqual("john@test.com", result.Value.Email);
	}

	[TestMethod]
	public async Task TransactionBuilder_InsertWithResultSelector_ThenUpdate_ChainsFromTypedInsert() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();

		// Act - InsertAsync<T> returns DbResult<T>, then chain to ThenUpdateAsync
		var result = await conn.ExecuteTransactionAsync(db =>
			db.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "John", Email = "john@test.com" },
				() => userId)
			.ThenUpdateAsync(
				"UPDATE Users SET Name = @Name WHERE Id = @Id",
				id => new { Id = id, Name = "Updated via chain" },
				userId)
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var name = conn.ExecuteScalar<string>("SELECT Name FROM Users WHERE Id = @Id", new { Id = userId });
		Assert.AreEqual("Updated via chain", name);
	}

	[TestMethod]
	public async Task TransactionBuilder_UpdateThenDelete_ChainsFromUpdate() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		// Act
		var result = await conn.ExecuteTransactionAsync(db =>
			db.UpdateAsync(
				"UPDATE Users SET Name = @Name WHERE Id = @Id",
				new { Id = userId, Name = "Jane" },
				userId)
			.ThenDeleteAsync(
				"DELETE FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var count = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Users WHERE Id = @Id", new { Id = userId });
		Assert.AreEqual(0L, count);
	}

	[TestMethod]
	public async Task TransactionBuilder_DeleteThenInsert_ChainsFromDelete() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var oldUserId = Guid.NewGuid().ToString();
		var newUserId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = oldUserId, Name = "OldUser", Email = "old@test.com" });

		// Act
		var result = await conn.ExecuteTransactionAsync(db =>
			db.DeleteAsync(
				"DELETE FROM Users WHERE Id = @Id",
				new { Id = oldUserId },
				oldUserId)
			.ThenInsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = newUserId, Name = "NewUser", Email = "new@test.com" })
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var oldCount = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Users WHERE Id = @Id", new { Id = oldUserId });
		var newCount = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Users WHERE Id = @Id", new { Id = newUserId });
		Assert.AreEqual(0L, oldCount);
		Assert.AreEqual(1L, newCount);
	}

	[TestMethod]
	public async Task TransactionBuilder_InsertThenUpdateThenGetScalar_ComplexChain() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		var orderId = Guid.NewGuid().ToString();

		// Act - Complex chain starting from InsertAsync
		var result = await conn.ExecuteTransactionAsync<long>(db =>
			db.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "John", Email = "john@test.com" })
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				new { Id = orderId, UserId = userId, Amount = 500.0 })
			.ThenUpdateAsync(
				"UPDATE Users SET Name = @Name WHERE Id = @Id",
				new { Id = userId, Name = "John (with order)" },
				userId)
			.ThenGetScalarAsync<long>("SELECT COUNT(*) FROM Orders")
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual(1L, result.Value);
		var userName = conn.ExecuteScalar<string>("SELECT Name FROM Users WHERE Id = @Id", new { Id = userId });
		Assert.AreEqual("John (with order)", userName);
	}

	#endregion

	#region Complex Chaining Patterns: Mutator -> Query -> Mutator -> Query

	[TestMethod]
	public async Task Insert_ThenGet_ThenUpdate_ChainsWithParametersFactory() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();

		// Act: Insert -> Get (using parametersFactory) -> Update (using parametersFactory)
		var result = await conn.ExecuteTransactionAsync(db =>
			db.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "John", Email = "john@test.com" },
				() => userId)
			.ThenGetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				id => new { Id = id },
				userId)
			.ThenUpdateAsync(
				"UPDATE Users SET Name = @Name WHERE Id = @Id",
				user => new { user.Id, Name = $"{user.Name} (verified)" },
				userId)
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var name = conn.ExecuteScalar<string>("SELECT Name FROM Users WHERE Id = @Id", new { Id = userId });
		Assert.AreEqual("John (verified)", name);
	}

	[TestMethod]
	public async Task Insert_ThenGetScalar_ThenInsert_ChainsWithScalarValue() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		var orderId = Guid.NewGuid().ToString();

		// Act: Insert user -> Get scalar count -> Insert order using count in amount
		var result = await conn.ExecuteTransactionAsync<string>(db =>
			db.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "John", Email = "john@test.com" },
				() => userId)
			.ThenGetScalarAsync<long>(
				"SELECT COUNT(*) FROM Users",
				_ => null)
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				count => new { Id = orderId, UserId = userId, Amount = (double)(count * 100) },
				() => orderId)
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual(orderId, result.Value);
		var amount = conn.ExecuteScalar<double>("SELECT Amount FROM Orders WHERE Id = @Id", new { Id = orderId });
		Assert.AreEqual(100.0, amount); // 1 user * 100
	}

	[TestMethod]
	public async Task Insert_ThenWhere_ThenUpdate_ChainsWithValidation() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();

		// Act: Insert -> Ensure (validate) -> Update
		var result = await conn.ExecuteTransactionAsync(db =>
			db.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "John", Email = "john@test.com" },
				() => new UserDto(userId, "John", "john@test.com"))
			.EnsureAsync(user => user.Email.Contains('@'), new BadRequestException("Invalid email"))
			.ThenUpdateAsync(
				"UPDATE Users SET Name = @Name WHERE Id = @Id",
				user => new { user.Id, Name = $"{user.Name} (email verified)" },
				userId)
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var name = conn.ExecuteScalar<string>("SELECT Name FROM Users WHERE Id = @Id", new { Id = userId });
		Assert.AreEqual("John (email verified)", name);
	}

	[TestMethod]
	public async Task Insert_ThenEnsure_Fails_ShortCircuits() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();

		// Act: Insert -> Ensure (fails) -> Update (should be skipped)
		var result = await conn.ExecuteTransactionAsync(db =>
			db.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "John", Email = "invalid-email" },
				() => new UserDto(userId, "John", "invalid-email"))
			.EnsureAsync(user => user.Email.Contains('@'), new BadRequestException("Invalid email"))
			.ThenUpdateAsync(
				"UPDATE Users SET Name = @Name WHERE Id = @Id",
				user => new { user.Id, Name = "Should not reach here" },
				userId)
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<BadRequestException>(result.Error);
		// Transaction was rolled back, so no user exists
		var count = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Users WHERE Id = @Id", new { Id = userId });
		Assert.AreEqual(0L, count); // Insert was rolled back
	}

	[TestMethod]
	public async Task Update_ThenGet_ThenDelete_ThenQueryAny() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId1 = Guid.NewGuid().ToString();
		var userId2 = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId1, Name = "John", Email = "john@test.com" });
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId2, Name = "Jane", Email = "jane@test.com" });

		// Act: Update -> Get -> Delete -> QueryAny
		var result = await conn.ExecuteTransactionAsync<IReadOnlyList<UserDto>>(db =>
			db.UpdateAsync(
				"UPDATE Users SET Name = @Name WHERE Id = @Id",
				new { Id = userId1, Name = "John Updated" },
				userId1,
				() => userId1)
			.ThenGetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				id => new { Id = id },
				userId1)
			.ThenDeleteAsync(
				"DELETE FROM Users WHERE Id = @Id",
				user => new { user.Id },
				userId1)
			.ThenQueryAnyAsync<UserDto>("SELECT Id, Name, Email FROM Users")
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.HasCount(1, result.Value); // Only Jane remains
		Assert.AreEqual("Jane", result.Value[0].Name);
	}

	[TestMethod]
	public async Task Insert_ThenQueryAny_ThenInsert_UsingQueryResults() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		var orderId = Guid.NewGuid().ToString();

		// Pre-insert a user
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = "existing-user", Name = "Existing", Email = "existing@test.com" });

		// Act: Insert new user -> QueryAny all users -> Insert order for first user
		var result = await conn.ExecuteTransactionAsync(db =>
			db.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "New User", Email = "new@test.com" },
				() => userId)
			.ThenQueryAnyAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users ORDER BY Name",
				_ => null)
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				users => new { Id = orderId, UserId = users[0].Id, Amount = users.Count * 50.0 })
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var order = conn.QueryFirst<OrderDto>("SELECT Id, UserId, Amount FROM Orders WHERE Id = @Id", new { Id = orderId });
		Assert.AreEqual(100.0, order.Amount); // 2 users * 50
	}

	[TestMethod]
	public async Task Delete_ThenGetScalar_ThenUpdate_ConditionalUpdate() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId1 = Guid.NewGuid().ToString();
		var userId2 = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId1, Name = "ToDelete", Email = "delete@test.com" });
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId2, Name = "ToKeep", Email = "keep@test.com" });

		// Act: Delete -> GetScalar (remaining count) -> Update remaining user's name with count
		var result = await conn.ExecuteTransactionAsync(db =>
			db.DeleteAsync(
				"DELETE FROM Users WHERE Id = @Id",
				new { Id = userId1 },
				userId1)
			.ThenGetScalarAsync<long>("SELECT COUNT(*) FROM Users")
			.ThenUpdateAsync(
				"UPDATE Users SET Name = @Name WHERE Id = @Id",
				count => new { Id = userId2, Name = $"Last of {count}" },
				userId2)
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var name = conn.ExecuteScalar<string>("SELECT Name FROM Users WHERE Id = @Id", new { Id = userId2 });
		Assert.AreEqual("Last of 1", name);
	}

	[TestMethod]
	public async Task Insert_ThenMap_ThenUpdate_TransformsValue() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();

		// Act: Insert -> Map to uppercase name -> Update with mapped value
		var result = await conn.ExecuteTransactionAsync(db =>
			db.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "john doe", Email = "john@test.com" },
				() => new UserDto(userId, "john doe", "john@test.com"))
			.MapAsync(user => user with { Name = user.Name.ToUpperInvariant() })
			.ThenUpdateAsync(
				"UPDATE Users SET Name = @Name WHERE Id = @Id",
				user => new { user.Id, user.Name },
				userId)
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var name = conn.ExecuteScalar<string>("SELECT Name FROM Users WHERE Id = @Id", new { Id = userId });
		Assert.AreEqual("JOHN DOE", name);
	}

	[TestMethod]
	public async Task Insert_ThenGet_ThenInsert_ThenGet_LongChain() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		var orderId = Guid.NewGuid().ToString();

		// Act: Insert user -> Get user -> Insert order -> Get order
		var result = await conn.ExecuteTransactionAsync<OrderDto>(db =>
			db.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "John", Email = "john@test.com" },
				() => userId)
			.ThenGetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				id => new { Id = id },
				userId)
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				user => new { Id = orderId, UserId = user.Id, Amount = 250.0 },
				() => orderId)
			.ThenGetAsync<OrderDto>(
				"SELECT Id, UserId, Amount FROM Orders WHERE Id = @Id",
				id => new { Id = id },
				orderId)
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual(orderId, result.Value.Id);
		Assert.AreEqual(userId, result.Value.UserId);
		Assert.AreEqual(250.0, result.Value.Amount);
	}

	[TestMethod]
	public async Task Update_ThenQueryAny_ThenDelete_BatchOperations() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId1 = Guid.NewGuid().ToString();
		var userId2 = Guid.NewGuid().ToString();
		var userId3 = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId1, Name = "Active1", Email = "active1@test.com" });
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId2, Name = "Inactive", Email = "inactive@test.com" });
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId3, Name = "Active2", Email = "active2@test.com" });

		// Act: Update one -> Query all -> Delete based on query (delete the first active)
		var result = await conn.ExecuteTransactionAsync(db =>
			db.UpdateAsync(
				"UPDATE Users SET Name = @Name WHERE Id = @Id",
				new { Id = userId2, Name = "StillInactive" },
				userId2,
				() => userId2)
			.ThenQueryAnyAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Name LIKE 'Active%' ORDER BY Name",
				_ => null)
			.ThenDeleteAsync(
				"DELETE FROM Users WHERE Id = @Id",
				activeUsers => new { activeUsers[0].Id },
				"first-active")
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var remainingCount = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Users");
		Assert.AreEqual(2L, remainingCount); // One was deleted
		var names = conn.Query<string>("SELECT Name FROM Users ORDER BY Name").ToList();
		Assert.Contains("Active2", names);
		Assert.Contains("StillInactive", names);
		Assert.DoesNotContain("Active1", names); // First active was deleted
	}

	[TestMethod]
	public async Task InsertWithResultSelector_ThenGetScalar_ThenUpdate_ThenGetScalar_MultipleScalars() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();

		// Act: Insert -> GetScalar (count before) -> Update -> GetScalar (count after, should be same)
		var result = await conn.ExecuteTransactionAsync<long>(db =>
			db.InsertAsync(
				"INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
				new { Id = userId, Name = "CountTest", Email = "count@test.com" },
				() => 0L) // Placeholder, we'll get actual count next
			.ThenGetScalarAsync<long>(
				"SELECT COUNT(*) FROM Users",
				_ => null)
			.ThenUpdateAsync(
				"UPDATE Users SET Name = @Name WHERE Id = @Id",
				count => new { Id = userId, Name = $"User #{count}" },
				userId,
				() => userId)
			.ThenGetScalarAsync<long>(
				"SELECT COUNT(*) FROM Users",
				_ => null)
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual(1L, result.Value); // Still 1 user
		var name = conn.ExecuteScalar<string>("SELECT Name FROM Users WHERE Id = @Id", new { Id = userId });
		Assert.AreEqual("User #1", name);
	}

	#endregion

	#region GetScalarAsync Null Handling

	[TestMethod]
	public async Task GetScalarAsync_WhenSqlReturnsNull_ReturnsFailureWithInvalidOperationException() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		// No users inserted, so MAX will return NULL

		// Act
		var result = await conn.GetScalarAsync<string>(
			"SELECT MAX(Name) FROM Users",
			cancellationToken: this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<InvalidOperationException>(result.Error);
		Assert.Contains("null", result.Error.Message.ToLowerInvariant());
	}

	[TestMethod]
	public async Task GetScalarAsync_WhenSqlReturnsNullableColumnWithValue_ReturnsSuccess() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = Guid.NewGuid().ToString(), Name = "John", Email = "john@test.com" });

		// Act
		var result = await conn.GetScalarAsync<string>(
			"SELECT MAX(Name) FROM Users",
			cancellationToken: this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual("John", result.Value);
	}

	[TestMethod]
	public async Task GetScalarAsync_WithCoalesceForNullColumn_ReturnsDefaultValue() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		// No users, MAX returns NULL, but COALESCE provides default

		// Act
		var result = await conn.GetScalarAsync<string>(
			"SELECT COALESCE(MAX(Name), 'No Users') FROM Users",
			cancellationToken: this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual("No Users", result.Value);
	}

	[TestMethod]
	public async Task GetScalarAsync_WithMapper_WhenSqlReturnsNull_MapperReceivesNull() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		// No users, MAX returns NULL
		var receivedValue = "not-null-sentinel";

		// Act
		var result = await conn.GetScalarAsync<string?, string>(
			"SELECT MAX(Name) FROM Users",
			value => {
				receivedValue = value;
				return value ?? "Default";
			},
			cancellationToken: this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.IsNull(receivedValue, "Mapper should receive null when SQL returns NULL");
		Assert.AreEqual("Default", result.Value);
	}

	[TestMethod]
	public async Task GetScalarAsync_WithMapper_WhenMapperReturnsNull_ReturnsFailure() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = Guid.NewGuid().ToString(), Name = "John", Email = "john@test.com" });

		// Act
		var result = await conn.GetScalarAsync<string?, string?>(
			"SELECT MAX(Name) FROM Users",
			_ => null!, // Mapper intentionally returns null
			cancellationToken: this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<InvalidOperationException>(result.Error);
		Assert.Contains("mapper", result.Error.Message.ToLowerInvariant());
	}

	[TestMethod]
	public async Task GetScalarAsync_WithMapper_WhenSqlReturnsValue_MapperTransformsIt() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = Guid.NewGuid().ToString(), Name = "John", Email = "john@test.com" });
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = Guid.NewGuid().ToString(), Name = "Jane", Email = "jane@test.com" });

		// Act
		var result = await conn.GetScalarAsync<long, string>(
			"SELECT COUNT(*) FROM Users",
			count => $"Total users: {count}",
			cancellationToken: this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual("Total users: 2", result.Value);
	}

	[TestMethod]
	public async Task GetScalarAsync_WithMapperAndParameters_WhenSqlReturnsNull_MapperReceivesNull() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		// Query for non-existent user, MAX returns NULL

		// Act
		var result = await conn.GetScalarAsync<string?, string>(
			"SELECT MAX(Name) FROM Users WHERE Id = @Id",
			new { Id = "nonexistent" },
			value => value ?? "Not Found",
			cancellationToken: this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual("Not Found", result.Value);
	}

	#endregion

	#region GetScalarAsync Null Handling in Transactions

	[TestMethod]
	public async Task TransactionContext_GetScalarAsync_WhenSqlReturnsNull_ReturnsFailure() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);

		// Act
		var result = await conn.ExecuteTransactionAsync<string>(db =>
			db.GetScalarAsync<string>("SELECT MAX(Name) FROM Users")
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<InvalidOperationException>(result.Error);
	}

	[TestMethod]
	public async Task TransactionContext_GetScalarAsync_WithMapper_MapperReceivesNull() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);

		// Act
		var result = await conn.ExecuteTransactionAsync<string>(db =>
			db.GetScalarAsync<string?, string>(
				"SELECT MAX(Name) FROM Users",
				value => value ?? "Empty Table")
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual("Empty Table", result.Value);
	}

	[TestMethod]
	public async Task FluentChaining_ThenGetScalarAsync_WhenSqlReturnsNull_PropagatesFailure() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		// Act - Get user, then try to get MAX of a non-existent related table data
		var result = await conn.ExecuteTransactionAsync<string>(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenGetScalarAsync<string>(
				"SELECT MAX(Name) FROM Users WHERE Id = 'nonexistent'") // Returns NULL
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<InvalidOperationException>(result.Error);
	}

	[TestMethod]
	public async Task FluentChaining_ThenGetScalarAsync_WithMapper_HandlesNull() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		// Act
		var result = await conn.ExecuteTransactionAsync<string>(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenGetScalarAsync<string?, string>(
				"SELECT MAX(Name) FROM Users WHERE Id = 'nonexistent'",
				value => value ?? "No matching users")
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual("No matching users", result.Value);
	}

	#endregion

	#region Conditional Operations (when: parameter)

	[TestMethod]
	public async Task ThenInsertAsync_WithWhenTrue_ExecutesInsert() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		var orderId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		// Act - Using generic ExecuteTransactionAsync because when: returns DbResult<T> (pass-through)
		var result = await conn.ExecuteTransactionAsync<UserDto>(ctx =>
			ctx.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				user => new { Id = orderId, UserId = user.Id, Amount = 100.0 },
				when: _ => true)  // Always execute
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var order = conn.QuerySingleOrDefault<OrderDto>(
			"SELECT Id, UserId, Amount FROM Orders WHERE Id = @Id",
			new { Id = orderId });
		Assert.IsNotNull(order);
		Assert.AreEqual(100.0, order.Amount);
	}

	[TestMethod]
	public async Task ThenInsertAsync_WithWhenFalse_SkipsInsert() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		var orderId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		// Act - Using generic ExecuteTransactionAsync because when: returns DbResult<T> (pass-through)
		var result = await conn.ExecuteTransactionAsync<UserDto>(ctx =>
			ctx.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				user => new { Id = orderId, UserId = user.Id, Amount = 100.0 },
				when: _ => false)  // Never execute
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var order = conn.QuerySingleOrDefault<OrderDto>(
			"SELECT Id, UserId, Amount FROM Orders WHERE Id = @Id",
			new { Id = orderId });
		Assert.IsNull(order, "Insert should have been skipped");
	}

	[TestMethod]
	public async Task ThenInsertAsync_WithWhenBasedOnValue_ConditionallExecutes() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		var orderId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		// Act - Only insert order if user name is "Admin"
		// Using generic ExecuteTransactionAsync because when: returns DbResult<T> (pass-through)
		var result = await conn.ExecuteTransactionAsync<UserDto>(ctx =>
			ctx.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				user => new { Id = orderId, UserId = user.Id, Amount = 100.0 },
				when: user => user.Name == "Admin")  // Only for Admin users
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var order = conn.QuerySingleOrDefault<OrderDto>(
			"SELECT Id, UserId, Amount FROM Orders WHERE Id = @Id",
			new { Id = orderId });
		Assert.IsNull(order, "Insert should have been skipped because user is not Admin");
	}

	[TestMethod]
	public async Task ThenInsertAsync_Generic_WithWhenFalse_ReturnsResultSelectorValue() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		var orderId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		// Act - when: with resultSelector, resultSelector is Func<T, TResult>
		var result = await conn.ExecuteTransactionAsync<string>(ctx =>
			ctx.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				user => new { Id = orderId, UserId = user.Id, Amount = 100.0 },
				_ => orderId,  // Func<T, TResult> - receives the current value
				when: _ => false)  // Skip insert
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual(orderId, result.Value, "Result selector should still be called");
		var order = conn.QuerySingleOrDefault<OrderDto>(
			"SELECT Id, UserId, Amount FROM Orders WHERE Id = @Id",
			new { Id = orderId });
		Assert.IsNull(order, "Insert should have been skipped");
	}

	[TestMethod]
	public async Task ThenUpdateAsync_WithWhenFalse_SkipsUpdate() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		// Act - Using generic ExecuteTransactionAsync because when: returns DbResult<T> (pass-through)
		var result = await conn.ExecuteTransactionAsync<UserDto>(ctx =>
			ctx.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenUpdateAsync(
				"UPDATE Users SET Name = @Name WHERE Id = @Id",
				user => new { user.Id, Name = "UpdatedName" },
				userId,
				when: _ => false)  // Never execute
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var user = conn.QuerySingle<UserDto>(
			"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
			new { Id = userId });
		Assert.AreEqual("John", user.Name, "Update should have been skipped");
	}

	[TestMethod]
	public async Task ThenUpdateAsync_WithWhenAndResultSelectorFalse_SkipsUpdate() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });


		// Act - Using generic ExecuteTransactionAsync because when: returns DbResult<T> (pass-through)
		var result = await conn.ExecuteTransactionAsync<UserLight>(ctx =>
			ctx.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenUpdateAsync(
				"UPDATE Users SET Name = @Name WHERE Id = @Id",
				user => new { user.Id, Name = "UpdatedName" },
				userId,
				user => new UserLight(user.Id, user.Name),  // Result selector
				when: _ => false)  // Never execute
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var user = conn.QuerySingle<UserLight>(
			"SELECT Id, Name FROM Users WHERE Id = @Id",
			new { Id = userId });
		Assert.AreEqual("John", user.Name, "Update should have been skipped");
	}

	[TestMethod]
	public async Task ThenUpdateAsync_WithWhenTrue_ExecutesUpdate() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		// Act - Using generic ExecuteTransactionAsync because when: returns DbResult<T> (pass-through)
		var result = await conn.ExecuteTransactionAsync<UserDto>(ctx =>
			ctx.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenUpdateAsync(
				"UPDATE Users SET Name = @Name WHERE Id = @Id",
				user => new { user.Id, Name = "UpdatedName" },
				userId,
				when: _ => true)  // Always execute
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var user = conn.QuerySingle<UserDto>(
			"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
			new { Id = userId });
		Assert.AreEqual("UpdatedName", user.Name);
	}

	[TestMethod]
	public async Task ThenUpdateAsync_WithWhenAndResultSelectorTrue_ExecutesUpdate() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		// Act - Using generic ExecuteTransactionAsync because when: returns DbResult<T> (pass-through)
		var result = await conn.ExecuteTransactionAsync<UserLight>(ctx =>
			ctx.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenUpdateAsync(
				"UPDATE Users SET Name = @Name WHERE Id = @Id",
				user => new { user.Id, Name = "UpdatedName" },
				userId,
				user => new UserLight(user.Id, user.Name),  // Result selector
				when: _ => true)  // Always execute
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var user = conn.QuerySingle<UserLight>(
			"SELECT Id, Name FROM Users WHERE Id = @Id",
			new { Id = userId });
		Assert.AreEqual("UpdatedName", user.Name);
	}

	[TestMethod]
	public async Task ThenDeleteAsync_WithWhenFalse_SkipsDelete() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		// Act - Using generic ExecuteTransactionAsync because when: returns DbResult<T> (pass-through)
		var result = await conn.ExecuteTransactionAsync<UserDto>(ctx =>
			ctx.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenDeleteAsync(
				"DELETE FROM Users WHERE Id = @Id",
				user => new { user.Id },
				userId,
				when: _ => false)  // Never execute
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var user = conn.QuerySingleOrDefault<UserDto>(
			"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
			new { Id = userId });
		Assert.IsNotNull(user, "Delete should have been skipped");
	}

	[TestMethod]
	public async Task ThenDeleteAsync_WithWhenTrue_ExecutesDelete() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		// Act - Using generic ExecuteTransactionAsync because when: returns DbResult<T> (pass-through)
		var result = await conn.ExecuteTransactionAsync<UserDto>(ctx =>
			ctx.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenDeleteAsync(
				"DELETE FROM Users WHERE Id = @Id",
				user => new { user.Id },
				userId,
				when: _ => true)  // Always execute
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var user = conn.QuerySingleOrDefault<UserDto>(
			"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
			new { Id = userId });
		Assert.IsNull(user, "User should be deleted");
	}

	[TestMethod]
	public async Task ConditionalOperations_ChainedWithMultipleConditions_ExecutesCorrectly() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		var orderId1 = Guid.NewGuid().ToString();
		var orderId2 = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "Admin", Email = "admin@test.com" });

		// Act - Insert first order only for Admin, skip second order
		// With when:, the current T value passes through automatically - no resultSelector needed
		var result = await conn.ExecuteTransactionAsync<UserDto>(ctx =>
			ctx.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				user => new { Id = orderId1, UserId = user.Id, Amount = 100.0 },
				when: user => user.Name == "Admin")  // Should execute, UserDto passes through
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				_ => new { Id = orderId2, UserId = userId, Amount = 200.0 },
				when: _ => false)  // Should skip, UserDto passes through
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);

		var order1 = conn.QuerySingleOrDefault<OrderDto>(
			"SELECT Id, UserId, Amount FROM Orders WHERE Id = @Id",
			new { Id = orderId1 });
		Assert.IsNotNull(order1, "First order should be inserted (Admin user)");

		var order2 = conn.QuerySingleOrDefault<OrderDto>(
			"SELECT Id, UserId, Amount FROM Orders WHERE Id = @Id",
			new { Id = orderId2 });
		Assert.IsNull(order2, "Second order should be skipped");
	}

	#endregion

	#region Non-Generic Conditional Operations (when: Func<bool>)

	[TestMethod]
	public async Task NonGenericDbResult_ThenInsertAsync_WithWhenTrue_ExecutesInsert() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		var orderId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		var shouldInsert = true;

		// Act - Start with insert (returns DbResult), then chain another insert with when:
		var result = await conn.ExecuteTransactionAsync(ctx =>
			ctx.InsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				new { Id = Guid.NewGuid().ToString(), UserId = userId, Amount = 50.0 })
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				new { Id = orderId, UserId = userId, Amount = 100.0 },
				when: () => shouldInsert)
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var count = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Orders WHERE UserId = @UserId", new { UserId = userId });
		Assert.AreEqual(2L, count, "Both orders should be inserted");
	}

	[TestMethod]
	public async Task NonGenericDbResult_ThenInsertAsync_WithWhenFalse_SkipsInsert() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		var orderId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		var shouldInsert = false;

		// Act
		var result = await conn.ExecuteTransactionAsync(ctx =>
			ctx.InsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				new { Id = Guid.NewGuid().ToString(), UserId = userId, Amount = 50.0 })
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				new { Id = orderId, UserId = userId, Amount = 100.0 },
				when: () => shouldInsert)  // External value
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var count = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Orders WHERE UserId = @UserId", new { UserId = userId });
		Assert.AreEqual(1L, count, "Only first order should be inserted");
	}

	[TestMethod]
	public async Task NonGenericDbResult_ThenInsertAsyncWithResultSelector_WithWhenFalse_SkipsButReturnsValue() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		var orderId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		var shouldInsert = false;

		// Act
		var result = await conn.ExecuteTransactionAsync<string>(ctx =>
			ctx.InsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				new { Id = Guid.NewGuid().ToString(), UserId = userId, Amount = 50.0 })
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				new { Id = orderId, UserId = userId, Amount = 100.0 },
				() => orderId,
				when: () => shouldInsert)
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual(orderId, result.Value, "Result selector should still return value");
		var order = conn.QuerySingleOrDefault<OrderDto>(
			"SELECT Id, UserId, Amount FROM Orders WHERE Id = @Id",
			new { Id = orderId });
		Assert.IsNull(order, "Second insert should have been skipped");
	}

	[TestMethod]
	public async Task NonGenericDbResult_ThenUpdateAsync_WithWhenFalse_SkipsUpdate() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		var shouldUpdate = false;

		// Act - Start with insert (returns DbResult), then chain update with when:
		var result = await conn.ExecuteTransactionAsync(ctx =>
			ctx.InsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				new { Id = Guid.NewGuid().ToString(), UserId = userId, Amount = 50.0 })
			.ThenUpdateAsync(
				"UPDATE Users SET Name = @Name WHERE Id = @Id",
				new { Id = userId, Name = "UpdatedName" },
				userId,
				when: () => shouldUpdate)
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var user = conn.QuerySingle<UserDto>(
			"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
			new { Id = userId });
		Assert.AreEqual("John", user.Name, "Update should have been skipped");
	}

	[TestMethod]
	public async Task NonGenericDbResult_ThenUpdateAsync_WithWhenTrue_ExecutesUpdate() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		var shouldUpdate = true;

		// Act
		var result = await conn.ExecuteTransactionAsync(ctx =>
			ctx.InsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				new { Id = Guid.NewGuid().ToString(), UserId = userId, Amount = 50.0 })
			.ThenUpdateAsync(
				"UPDATE Users SET Name = @Name WHERE Id = @Id",
				new { Id = userId, Name = "UpdatedName" },
				userId,
				when: () => shouldUpdate)
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var user = conn.QuerySingle<UserDto>(
			"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
			new { Id = userId });
		Assert.AreEqual("UpdatedName", user.Name);
	}

	[TestMethod]
	public async Task NonGenericDbResult_ThenDeleteAsync_WithWhenFalse_SkipsDelete() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		var shouldDelete = false;

		// Act
		var result = await conn.ExecuteTransactionAsync(ctx =>
			ctx.InsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				new { Id = Guid.NewGuid().ToString(), UserId = userId, Amount = 50.0 })
			.ThenDeleteAsync(
				"DELETE FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId,
				when: () => shouldDelete)
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var user = conn.QuerySingleOrDefault<UserDto>(
			"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
			new { Id = userId });
		Assert.IsNotNull(user, "Delete should have been skipped");
	}

	[TestMethod]
	public async Task NonGenericDbResult_ThenDeleteAsync_WithWhenTrue_ExecutesDelete() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		var orderId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		var shouldDelete = true;

		// Act - Insert order, then delete the order (not the user, to avoid FK constraint)
		var result = await conn.ExecuteTransactionAsync(ctx =>
			ctx.InsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				new { Id = orderId, UserId = userId, Amount = 50.0 })
			.ThenDeleteAsync(
				"DELETE FROM Orders WHERE Id = @Id",
				new { Id = orderId },
				orderId,
				when: () => shouldDelete)
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var order = conn.QuerySingleOrDefault<OrderDto>(
			"SELECT Id, UserId, Amount FROM Orders WHERE Id = @Id",
			new { Id = orderId });
		Assert.IsNull(order, "Order should be deleted");
	}

	[TestMethod]
	public async Task NonGenericDbResult_ChainedConditionalOperations_HandlerScenario() {
		// Arrange - This simulates the Handler scenario where incoming request has a bool
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		var orderId = Guid.NewGuid().ToString();
		var auditId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		// Simulate incoming request with ShouldAudit property
		var request = new { ShouldAudit = true };

		// Act - Insert order, then conditionally insert audit log based on request property
		var result = await conn.ExecuteTransactionAsync(ctx =>
			ctx.InsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				new { Id = orderId, UserId = userId, Amount = 100.0 })
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				new { Id = auditId, UserId = userId, Amount = 0.0 },  // Using Orders as "audit log" for simplicity
				when: () => request.ShouldAudit)  // Closure captures external value
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var count = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Orders WHERE UserId = @UserId", new { UserId = userId });
		Assert.AreEqual(2L, count, "Both order and audit should be inserted");
	}

	[TestMethod]
	public async Task NonGenericDbResult_ChainedConditionalOperations_HandlerScenario_WhenFalse() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		var orderId = Guid.NewGuid().ToString();
		var auditId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "John", Email = "john@test.com" });

		// Simulate incoming request with ShouldAudit = false
		var request = new { ShouldAudit = false };

		// Act
		var result = await conn.ExecuteTransactionAsync(ctx =>
			ctx.InsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				new { Id = orderId, UserId = userId, Amount = 100.0 })
			.ThenInsertAsync(
				"INSERT INTO Orders (Id, UserId, Amount) VALUES (@Id, @UserId, @Amount)",
				new { Id = auditId, UserId = userId, Amount = 0.0 },
				when: () => request.ShouldAudit)
		, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		var count = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Orders WHERE UserId = @UserId", new { UserId = userId });
		Assert.AreEqual(1L, count, "Only order should be inserted, audit skipped");
	}

	#endregion

	#region Test DTOs

	private record UserDto(string Id, string Name, string Email);
	private record User(string Id, string Name, string Email);
	private record UserLight(string Id, string Name);
	private record OrderDto(string Id, string UserId, double Amount);

	public TestContext TestContext { get; set; }

	#endregion

}
