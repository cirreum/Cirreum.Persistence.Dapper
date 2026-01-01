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
		var result = await conn.ExecuteInTransactionAsync<UserDto>(db =>
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
		var result = await conn.ExecuteInTransactionAsync(async db => {
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
		var result = await conn.ExecuteInTransactionAsync(async db => {
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
		var result = await conn.ExecuteInTransactionAsync<User>(db =>
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
		var result = await conn.ExecuteInTransactionAsync<UserDto>(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.WhereAsync(u => u.Name.StartsWith("Valid"), new BadRequestException("Invalid user"))
, this.TestContext.CancellationToken);

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual("ValidUser", result.Value.Name);
	}

	[TestMethod]
	public async Task FluentChaining_GetThenWhere_FailsFilter() {
		// Arrange
		using var conn = CreateConnection();
		CreateTestSchema(conn);
		var userId = Guid.NewGuid().ToString();
		conn.Execute("INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email)",
			new { Id = userId, Name = "InvalidUser", Email = "invalid@test.com" });
		var expectedError = new BadRequestException("User must start with 'Valid'");

		// Act
		var result = await conn.ExecuteInTransactionAsync<UserDto>(db =>
			db.GetAsync<UserDto>(
				"SELECT Id, Name, Email FROM Users WHERE Id = @Id",
				new { Id = userId },
				userId)
			.WhereAsync(u => u.Name.StartsWith("Valid"), expectedError)
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
		var result = await conn.ExecuteInTransactionAsync<long>(db =>
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
		var result = await conn.ExecuteInTransactionAsync<IReadOnlyList<OrderDto>>(db =>
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
		var result = await conn.ExecuteInTransactionAsync<long>(db =>
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
		var result = await conn.ExecuteInTransactionAsync<UserDto>(db =>
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
		var result = await conn.ExecuteInTransactionAsync(db =>
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
		var result = await conn.ExecuteInTransactionAsync(db =>
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
		var result = await conn.ExecuteInTransactionAsync<string>(db =>
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
		var result = await conn.ExecuteInTransactionAsync<(string OrderId, string UserName)>(db =>
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
		var result = await conn.ExecuteInTransactionAsync<long>(db =>
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
		var result = await conn.ExecuteInTransactionAsync(db =>
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
		var result = await conn.ExecuteInTransactionAsync(db =>
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
		var result = await conn.ExecuteInTransactionAsync(db =>
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
		var result = await conn.ExecuteInTransactionAsync<string>(db =>
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
		var result = await conn.ExecuteInTransactionAsync(db =>
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
		var result = await conn.ExecuteInTransactionAsync(db =>
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
		var result = await conn.ExecuteInTransactionAsync(db =>
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
		var result = await conn.ExecuteInTransactionAsync<long>(db =>
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
		var result = await conn.ExecuteInTransactionAsync<string>(db =>
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

	#region Test DTOs

	private record UserDto(string Id, string Name, string Email);
	private record User(string Id, string Name, string Email);
	private record OrderDto(string Id, string UserId, double Amount);

	public TestContext TestContext { get; set; }

	#endregion

}
