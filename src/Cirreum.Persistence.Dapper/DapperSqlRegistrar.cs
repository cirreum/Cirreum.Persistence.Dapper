namespace Cirreum.Persistence;

using Cirreum.Persistence.Configuration;
using Cirreum.Persistence.Extensions;
using Cirreum.Persistence.Health;
using Cirreum.Providers;
using Cirreum.ServiceProvider;
using Cirreum.ServiceProvider.Health;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service provider registrar for Dapper SQL Server persistence.
/// </summary>
/// <remarks>
/// <para>
/// This registrar integrates SQL Server database connections into the Cirreum service provider framework,
/// enabling dependency injection of <see cref="IDbConnectionFactory"/> instances with support for:
/// </para>
/// <list type="bullet">
///   <item><description>Azure AD (Entra ID) authentication via <c>DefaultAzureCredential</c></description></item>
///   <item><description>Multiple named instances using keyed DI services</description></item>
///   <item><description>Configurable service lifetimes (Singleton, Scoped, Transient)</description></item>
///   <item><description>Health check integration with customizable queries</description></item>
/// </list>
/// </remarks>
/// <seealso cref="IDbConnectionFactory"/>
/// <seealso cref="DapperSqlSettings"/>
/// <seealso cref="DapperSqlInstanceSettings"/>
public sealed class DapperSqlRegistrar() :
	ServiceProviderRegistrar<
		DapperSqlSettings,
		DapperSqlInstanceSettings,
		DapperSqlHealthCheckOptions> {

	/// <inheritdoc/>
	public override ProviderType ProviderType { get; } = ProviderType.Persistence;

	/// <summary>
	/// Gets the name of the data provider associated with this implementation.
	/// </summary>
	public override string ProviderName { get; } = "Dapper";

	/// <inheritdoc/>
	public override string[] ActivitySourceNames { get; } = [
		"Microsoft.Data.SqlClient",
		"Cirreum.Persistence.Dapper"
	];

	/// <inheritdoc/>
	protected override void AddServiceProviderInstance(
		IServiceCollection services,
		string serviceKey,
		DapperSqlInstanceSettings settings) {
		services.AddDbFactories(serviceKey, settings);
	}

	/// <inheritdoc/>
	protected override IServiceProviderHealthCheck<DapperSqlHealthCheckOptions> CreateHealthCheck(
		IServiceProvider serviceProvider,
		string serviceKey,
		DapperSqlInstanceSettings settings) {
		return serviceProvider.CreateDapperSqlHealthCheck(settings);
	}

}