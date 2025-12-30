namespace Cirreum.Persistence.Internal;

using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;

using System.Data.Common;

/// <summary>
/// SQL Server connection factory with Azure authentication support.
/// </summary>
internal sealed class DapperSqlConnectionFactory : IDbConnectionFactory {

	private readonly string _connectionString;
	private readonly bool _useAzureAdAuth;
	private readonly int _commandTimeoutSeconds;

	public DapperSqlConnectionFactory(DapperSqlOptions options) {
		_connectionString = options.ConnectionString
			?? throw new InvalidOperationException("ConnectionString is required.");
		_useAzureAdAuth = options.UseAzureAuthentication;
		_commandTimeoutSeconds = options.CommandTimeoutSeconds;

		if (_useAzureAdAuth) {
			// Ensure Integrated Security is not set when using Azure AD
			var builder = new SqlConnectionStringBuilder(_connectionString) {
				IntegratedSecurity = false
			};
			_connectionString = builder.ConnectionString;
		}
	}

	/// <inheritdoc />
	public int CommandTimeoutSeconds => _commandTimeoutSeconds;

	/// <inheritdoc />
	public async Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default) {
		SqlConnection connection;

		if (_useAzureAdAuth) {
			var credential = new DefaultAzureCredential();
			var token = await credential.GetTokenAsync(
				new TokenRequestContext(["https://database.windows.net/.default"]),
				cancellationToken);

			connection = new SqlConnection(_connectionString) {
				AccessToken = token.Token
			};
		} else {
			connection = new SqlConnection(_connectionString);
		}

		await connection.OpenAsync(cancellationToken);
		return connection;
	}

}