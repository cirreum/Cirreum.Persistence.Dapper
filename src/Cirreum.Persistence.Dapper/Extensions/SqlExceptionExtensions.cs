namespace Microsoft.Data.SqlClient;

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

	}

}