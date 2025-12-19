namespace Cirreum.Persistence.Configuration;

using Cirreum.Persistence.Health;
using Cirreum.ServiceProvider.Configuration;

/// <summary>
/// Instance-specific settings for a Dapper SQL Server database connection.
/// </summary>
/// <remarks>
/// <para>
/// Each instance represents a single database connection configuration, allowing for multiple
/// database connections within the same application. Settings include authentication mode
/// and command timeout.
/// </para>
/// <para>
/// For Azure AD authentication, set <see cref="UseAzureAdAuthentication"/> to <c>true</c>.
/// The factory will use <c>DefaultAzureCredential</c> to obtain access tokens automatically.
/// </para>
/// <para>
/// The connection factory is registered as a singleton. Individual connections are short-lived
/// and managed by ADO.NET connection pooling.
/// </para>
/// </remarks>
/// <seealso cref="DapperSqlSettings"/>
/// <seealso cref="IDbConnectionFactory"/>
public sealed class DapperSqlInstanceSettings :
	ServiceProviderInstanceSettings<DapperSqlHealthCheckOptions> {

	/// <summary>
	/// Whether to use Azure AD (Entra ID) authentication.
	/// When enabled, uses DefaultAzureCredential for token-based auth.
	/// </summary>
	public bool UseAzureAdAuthentication { get; set; }

	/// <summary>
	/// Command timeout in seconds. Default is 30.
	/// </summary>
	public int CommandTimeoutSeconds { get; set; } = 30;

	/// <inheritdoc/>
	public override DapperSqlHealthCheckOptions? HealthOptions { get; set; }

}
