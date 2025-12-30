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
	/// Whether to use Azure authentication.
	/// </summary>
	public bool UseAzureAuthentication { get; set; }

	/// <summary>
	/// Command timeout in seconds. Default is 30.
	/// </summary>
	public int CommandTimeoutSeconds { get; set; } = 30;

}
