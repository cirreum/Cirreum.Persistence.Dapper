namespace Cirreum.Persistence.Health;

using Cirreum.Persistence.Internal;
using Cirreum.ServiceProvider.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Health check for SQL Server connections.
/// </summary>
internal sealed class DapperSqlHealthCheck(
	DapperSqlOptions options,
	DapperSqlHealthCheckOptions healthOptions
) : IServiceProviderHealthCheck<DapperSqlHealthCheckOptions> {

	/// <inheritdoc/>
	public DapperSqlHealthCheckOptions HealthOptions => healthOptions;

	/// <inheritdoc/>
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default) {

		try {
			var factory = new DapperSqlConnectionFactory(options);
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);

			await using var command = connection.CreateCommand();
			command.CommandText = healthOptions.Query;
			command.CommandTimeout = (int)(healthOptions.Timeout?.TotalSeconds ?? 5);

			await command.ExecuteScalarAsync(cancellationToken);

			return HealthCheckResult.Healthy("SQL Server connection is healthy.");
		} catch (Exception ex) {
			return HealthCheckResult.Unhealthy(
				"SQL Server connection failed.",
				exception: ex);
		}
	}

}