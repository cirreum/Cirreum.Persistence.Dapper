namespace Cirreum.Persistence;

using System.Data.Common;

/// <summary>
/// Factory for creating database connections.
/// </summary>
/// <remarks>
/// <para>
/// Implementations should return open, ready-to-use connections from a connection pool.
/// Callers are responsible for disposing the returned connection.
/// </para>
/// <para>
/// <strong>Direct usage:</strong>
/// </para>
/// <code>
/// await using var conn = await factory.CreateConnectionAsync(cancellationToken);
/// return await conn.GetAsync&lt;Order&gt;(sql, parameters, key, cancellationToken: cancellationToken);
/// </code>
/// <para>
/// <strong>Using extension methods:</strong>
/// </para>
/// <code>
/// // Single operation (connection lifecycle managed automatically)
/// return await factory.ExecuteAsync(conn => 
///     conn.GetAsync&lt;Order&gt;(sql, parameters, key), cancellationToken);
/// 
/// // Transaction (connection and transaction lifecycle managed automatically)
/// return await factory.ExecuteTransactionAsync(ctx => ctx
///     .InsertAsync(orderSql, orderParam)
///     .ThenInsertAsync(lineItemSql, lineItemParam)
/// , cancellationToken);
/// </code>
/// </remarks>
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
