namespace Cirreum.Persistence;

using System.Data.Common;

/// <summary>
/// Factory for creating database connections.
/// </summary>
public interface IDbConnectionFactory {

	/// <summary>
	/// Creates and opens a new database connection.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>An open database connection.</returns>
	Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// The command timeout in seconds.
	/// </summary>
	int CommandTimeoutSeconds { get; }

}
