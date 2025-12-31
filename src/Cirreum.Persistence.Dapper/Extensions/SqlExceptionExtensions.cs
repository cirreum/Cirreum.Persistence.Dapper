namespace Cirreum.Persistence;

using Cirreum.Exceptions;
using Microsoft.Data.SqlClient;

/// <summary>
/// Extension methods for inspecting SQL Server exceptions.
/// </summary>
public static class SqlExceptionExtensions {

	/// <summary>
	/// SQL Server error number for unique constraint violation.
	/// </summary>
	private const int UniqueConstraintViolation = 2627;

	/// <summary>
	/// SQL Server error number for unique index violation.
	/// </summary>
	private const int UniqueIndexViolation = 2601;

	/// <summary>
	/// SQL Server error number for foreign key violation.
	/// </summary>
	private const int ForeignKeyViolation = 547;

	extension(SqlException ex) {

		/// <summary>
		/// Determines whether the exception is a unique constraint or unique index violation.
		/// </summary>
		/// <returns>True if the exception indicates a duplicate key violation; otherwise, false.</returns>
		public bool IsUniqueConstraintViolation() =>
			ex.Number is UniqueConstraintViolation or UniqueIndexViolation;

		/// <summary>
		/// Determines whether the exception is a foreign key constraint violation.
		/// </summary>
		/// <returns>True if the exception indicates a foreign key violation; otherwise, false.</returns>
		public bool IsForeignKeyViolation() =>
			ex.Number == ForeignKeyViolation;

		/// <summary>
		/// Determines whether the exception is any constraint violation (unique, FK, etc).
		/// </summary>
		/// <returns>True if the exception indicates any constraint violation; otherwise, false.</returns>
		public bool IsConstraintViolation() =>
			ex.Number is UniqueConstraintViolation or UniqueIndexViolation or ForeignKeyViolation;

		/// <summary>
		/// Converts the <see cref="SqlException"/> to an appropriate <see cref="Result"/> based on the error type.
		/// </summary>
		/// <remarks>
		/// Unique constraint violations become <see cref="AlreadyExistsException"/> (HTTP 409).
		/// Foreign key violations become <see cref="BadRequestException"/> (HTTP 400).
		/// All other exceptions are returned as-is (HTTP 500).
		/// </remarks>
		/// <returns>A failed <see cref="Result"/> with an appropriate exception type.</returns>
		public Result ToResult() {
			if (ex.IsUniqueConstraintViolation()) {
				return Result.AlreadyExist(ex.Message);
			}
			if (ex.IsForeignKeyViolation()) {
				return Result.Conflict(ex.Message);
			}
			return Result.Fail(ex);
		}

		/// <summary>
		/// Converts the <see cref="SqlException"/> to an appropriate <see cref="Result{T}"/> based on the error type.
		/// </summary>
		/// <remarks>
		/// Unique constraint violations become <see cref="AlreadyExistsException"/> (HTTP 409).
		/// Foreign key violations become <see cref="BadRequestException"/> (HTTP 400).
		/// All other exceptions are returned as-is (HTTP 500).
		/// </remarks>
		/// <typeparam name="T">The type of the value that would have been returned on success.</typeparam>
		/// <returns>A failed <see cref="Result{T}"/> with an appropriate exception type.</returns>
		public Result<T> ToResult<T>() {
			if (ex.IsUniqueConstraintViolation()) {
				return Result.AlreadyExist<T>(ex.Message);
			}
			if (ex.IsForeignKeyViolation()) {
				return Result.BadRequest<T>(ex.Message);
			}
			return Result.Fail<T>(ex);
		}

	}

}