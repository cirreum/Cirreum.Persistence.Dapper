namespace Cirreum.Persistence.Configuration;

using Cirreum.Persistence.Health;
using Cirreum.ServiceProvider.Configuration;

/// <summary>
/// Configuration settings container for Dapper SQL Server persistence provider.
/// </summary>
/// <remarks>
/// This class serves as the root configuration type for the Dapper SQL provider,
/// containing a collection of <see cref="DapperSqlInstanceSettings"/> for configuring
/// multiple database instances.
/// </remarks>
/// <seealso cref="DapperSqlInstanceSettings"/>
/// <seealso cref="DapperSqlHealthCheckOptions"/>
public sealed class DapperSqlSettings :
	ServiceProviderSettings<
		DapperSqlInstanceSettings,
		DapperSqlHealthCheckOptions>;
