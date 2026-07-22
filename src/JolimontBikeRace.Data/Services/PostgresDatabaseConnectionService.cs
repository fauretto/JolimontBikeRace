using JolimontBikeRace.Core.Interfaces;
using JolimontBikeRace.Data.Interfaces;
using Npgsql;

namespace JolimontBikeRace.Data.Services;

/// <summary>
/// Verifies connectivity to the PostgreSQL database by opening a connection and executing a
/// trivial query.
/// </summary>
public class PostgresDatabaseConnectionService : IDatabaseConnectionService
{
    private readonly IConnectionStringProvider _connectionStringProvider;
    private readonly ILogService _logService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresDatabaseConnectionService"/> class.
    /// </summary>
    /// <param name="connectionStringProvider">The provider exposing the connection string to use.</param>
    /// <param name="logService">The logging service used to record the outcome of connection attempts.</param>
    public PostgresDatabaseConnectionService(IConnectionStringProvider connectionStringProvider, ILogService logService)
    {
        _connectionStringProvider = connectionStringProvider;
        _logService = logService;
    }

    /// <summary>
    /// Gets the name of the database that this service connects to, extracted from the
    /// connection string, used for display purposes in the user interface.
    /// </summary>
    public string DatabaseName
    {
        get
        {
            var builder = new NpgsqlConnectionStringBuilder(_connectionStringProvider.ConnectionString);
            return builder.Database ?? string.Empty;
        }
    }

    /// <summary>
    /// Attempts to open a connection to the database and to execute a trivial query, in order to
    /// verify that the database is reachable and correctly configured.
    /// </summary>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync();

            _logService.Information(
                "PostgresDatabaseConnectionService -> TestConnectionAsync",
                $"Successfully connected to database '{DatabaseName}'.");
            return true;
        }
        catch (Exception exception)
        {
            _logService.Error(
                "PostgresDatabaseConnectionService -> TestConnectionAsync",
                $"Failed to connect to database '{DatabaseName}'.",
                exception);
            return false;
        }
    }
}
