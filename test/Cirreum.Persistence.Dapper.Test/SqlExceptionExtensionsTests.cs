namespace Cirreum.Persistence.Dapper.Test;

using Cirreum.Exceptions;
using Cirreum.Persistence;
using Microsoft.Data.SqlClient;
using System.Reflection;

[TestClass]
public sealed class SqlExceptionExtensionsTests {

	#region Test Helper

	/// <summary>
	/// Creates a SqlException with the specified error number for testing.
	/// SqlException is sealed and has no public constructor, so reflection is required.
	/// </summary>
	private static SqlException CreateSqlException(int errorNumber, string message = "Test error") {
		var collection = ConstructSqlErrorCollection();
		var error = ConstructSqlError(errorNumber, message);

		var addMethod = typeof(SqlErrorCollection)
			.GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!;
		addMethod.Invoke(collection, [error]);

		var createMethod = typeof(SqlException)
			.GetMethod(
				"CreateException",
				BindingFlags.NonPublic | BindingFlags.Static,
				null,
				[typeof(SqlErrorCollection), typeof(string)],
				null)!;

		return (SqlException)createMethod.Invoke(null, [collection, "11.0.0"])!;
	}

	private static SqlErrorCollection ConstructSqlErrorCollection() {
		var ctor = typeof(SqlErrorCollection)
			.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0];
		return (SqlErrorCollection)ctor.Invoke(null);
	}

	private static SqlError ConstructSqlError(int errorNumber, string message) {
		// SqlError constructor signatures in Microsoft.Data.SqlClient 6.x:
		// (Int32 infoNumber, Byte errorState, Byte errorClass, String server, String errorMessage, String procedure, Int32 lineNumber, Int32 win32ErrorCode, Exception exception)
		// (Int32 infoNumber, Byte errorState, Byte errorClass, String server, String errorMessage, String procedure, Int32 lineNumber, Exception exception)
		var ctors = typeof(SqlError).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance);

		// Prefer the 9-parameter constructor with win32ErrorCode
		var ctor = ctors.FirstOrDefault(c => c.GetParameters().Length == 9)
			?? ctors.FirstOrDefault(c => c.GetParameters().Length == 8)
			?? ctors.First();

		var parameters = ctor.GetParameters();
		var args = new object?[parameters.Length];

		for (var i = 0; i < parameters.Length; i++) {
			var param = parameters[i];
			args[i] = param.Name switch {
				"infoNumber" => errorNumber,
				"errorState" => (byte)0,
				"errorClass" => (byte)0,
				"server" => "TestServer",
				"errorMessage" => message,
				"procedure" => "TestProcedure",
				"lineNumber" => 0,
				"win32ErrorCode" => 0,
				"batchIndex" => 0,
				"exception" => null,
				_ => null
			};
		}

		return (SqlError)ctor.Invoke(args);
	}

	#endregion

	#region IsUniqueConstraintViolation

	[TestMethod]
	public void IsUniqueConstraintViolation_WhenErrorNumber2627_ReturnsTrue() {
		// Arrange
		var ex = CreateSqlException(2627, "Violation of PRIMARY KEY constraint");

		// Act
		var result = ex.IsUniqueConstraintViolation();

		// Assert
		Assert.IsTrue(result);
	}

	[TestMethod]
	public void IsUniqueConstraintViolation_WhenErrorNumber2601_ReturnsTrue() {
		// Arrange
		var ex = CreateSqlException(2601, "Cannot insert duplicate key");

		// Act
		var result = ex.IsUniqueConstraintViolation();

		// Assert
		Assert.IsTrue(result);
	}

	[TestMethod]
	public void IsUniqueConstraintViolation_WhenOtherError_ReturnsFalse() {
		// Arrange
		var ex = CreateSqlException(547, "Foreign key violation");

		// Act
		var result = ex.IsUniqueConstraintViolation();

		// Assert
		Assert.IsFalse(result);
	}

	#endregion

	#region IsForeignKeyViolation

	[TestMethod]
	public void IsForeignKeyViolation_WhenErrorNumber547_ReturnsTrue() {
		// Arrange
		var ex = CreateSqlException(547, "FOREIGN KEY constraint violation");

		// Act
		var result = ex.IsForeignKeyViolation();

		// Assert
		Assert.IsTrue(result);
	}

	[TestMethod]
	public void IsForeignKeyViolation_WhenOtherError_ReturnsFalse() {
		// Arrange
		var ex = CreateSqlException(2627, "Unique constraint violation");

		// Act
		var result = ex.IsForeignKeyViolation();

		// Assert
		Assert.IsFalse(result);
	}

	#endregion

	#region IsConstraintViolation

	[TestMethod]
	public void IsConstraintViolation_WhenUniqueConstraint_ReturnsTrue() {
		// Arrange
		var ex = CreateSqlException(2627);

		// Act
		var result = ex.IsConstraintViolation();

		// Assert
		Assert.IsTrue(result);
	}

	[TestMethod]
	public void IsConstraintViolation_WhenUniqueIndex_ReturnsTrue() {
		// Arrange
		var ex = CreateSqlException(2601);

		// Act
		var result = ex.IsConstraintViolation();

		// Assert
		Assert.IsTrue(result);
	}

	[TestMethod]
	public void IsConstraintViolation_WhenForeignKey_ReturnsTrue() {
		// Arrange
		var ex = CreateSqlException(547);

		// Act
		var result = ex.IsConstraintViolation();

		// Assert
		Assert.IsTrue(result);
	}

	[TestMethod]
	public void IsConstraintViolation_WhenOtherError_ReturnsFalse() {
		// Arrange
		var ex = CreateSqlException(1234, "Some other error");

		// Act
		var result = ex.IsConstraintViolation();

		// Assert
		Assert.IsFalse(result);
	}

	#endregion

	#region ToResult (non-generic)

	[TestMethod]
	public void ToResult_WhenUniqueConstraintViolation_ReturnsAlreadyExistsException() {
		// Arrange
		var ex = CreateSqlException(2627, "Duplicate key error");

		// Act
		var result = ex.ToResult();

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<AlreadyExistsException>(result.Error);
	}

	[TestMethod]
	public void ToResult_WhenUniqueIndexViolation_ReturnsAlreadyExistsException() {
		// Arrange
		var ex = CreateSqlException(2601, "Duplicate index error");

		// Act
		var result = ex.ToResult();

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<AlreadyExistsException>(result.Error);
	}

	[TestMethod]
	public void ToResult_WhenForeignKeyViolation_ReturnsConflictException() {
		// Arrange
		var ex = CreateSqlException(547, "FK constraint error");

		// Act
		var result = ex.ToResult();

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<ConflictException>(result.Error);
	}

	[TestMethod]
	public void ToResult_WhenOtherError_ReturnsOriginalException() {
		// Arrange
		var ex = CreateSqlException(1234, "Some other error");

		// Act
		var result = ex.ToResult();

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.AreSame(ex, result.Error);
	}

	#endregion

	#region ToResult<T> (generic)

	[TestMethod]
	public void ToResultT_WhenUniqueConstraintViolation_ReturnsAlreadyExistsException() {
		// Arrange
		var ex = CreateSqlException(2627, "Duplicate key error");

		// Act
		var result = ex.ToResult<int>();

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<AlreadyExistsException>(result.Error);
	}

	[TestMethod]
	public void ToResultT_WhenUniqueIndexViolation_ReturnsAlreadyExistsException() {
		// Arrange
		var ex = CreateSqlException(2601, "Duplicate index error");

		// Act
		var result = ex.ToResult<string>();

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<AlreadyExistsException>(result.Error);
	}

	[TestMethod]
	public void ToResultT_WhenForeignKeyViolation_ReturnsBadRequestException() {
		// Arrange
		var ex = CreateSqlException(547, "FK constraint error");

		// Act
		var result = ex.ToResult<object>();

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.IsInstanceOfType<BadRequestException>(result.Error);
	}

	[TestMethod]
	public void ToResultT_WhenOtherError_ReturnsOriginalException() {
		// Arrange
		var ex = CreateSqlException(9999, "Unknown error");

		// Act
		var result = ex.ToResult<double>();

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.AreSame(ex, result.Error);
	}

	#endregion

}
