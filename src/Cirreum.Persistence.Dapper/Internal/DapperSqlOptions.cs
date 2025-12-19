namespace Cirreum.Persistence.Internal;

/// <summary>
/// Options for SQL Server connection.
/// </summary>
public sealed class DapperSqlOptions {

	/// <summary>
	/// The connection string.
	/// </summary>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Whether to use Azure AD authentication.
	/// </summary>
	public bool UseAzureAdAuthentication { get; set; }

	/// <summary>
	/// Command timeout in seconds. Default is 30.
	/// </summary>
	public int CommandTimeoutSeconds { get; set; } = 30;

}
