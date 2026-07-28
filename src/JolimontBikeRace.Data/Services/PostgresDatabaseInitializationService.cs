using JolimontBikeRace.Core.Interfaces;
using JolimontBikeRace.Data.Interfaces;
using Npgsql;

namespace JolimontBikeRace.Data.Services;

/// <summary>
/// Ensures that the PostgreSQL database used by the application exists, creating it and applying
/// the embedded schema script when it is missing. Existence is checked and the database is created
/// through a connection to the server's "postgres" maintenance database, because a database cannot
/// be created while connected to it, and a connection cannot be opened to a database that does not
/// yet exist.
/// </summary>
public class PostgresDatabaseInitializationService : IDatabaseInitializationService
{
    private const string MaintenanceDatabaseName = "postgres";
    private const string SchemaResourceName = "JolimontBikeRace.Data.create_jolimontbikerace_schema.sql";

    private readonly IConnectionStringProvider _connectionStringProvider;
    private readonly ILogService _logService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresDatabaseInitializationService"/> class.
    /// </summary>
    /// <param name="connectionStringProvider">The provider exposing the application connection string, whose database name identifies the database to ensure.</param>
    /// <param name="logService">The logging service used to record the outcome of the check and any creation.</param>
    public PostgresDatabaseInitializationService(IConnectionStringProvider connectionStringProvider, ILogService logService)
    {
        _connectionStringProvider = connectionStringProvider;
        _logService = logService;
    }

    /// <summary>
    /// Verifies that the target database exists and, when it does not, creates it and applies the
    /// schema. When applying the schema fails, the partially created database is dropped so that a
    /// broken, half-created database is never left behind.
    /// </summary>
    /// <returns>The outcome describing whether the database already existed or was created.</returns>
    public async Task<DatabaseInitializationOutcome> EnsureDatabaseExistsAsync()
    {
        var applicationBuilder = new NpgsqlConnectionStringBuilder(_connectionStringProvider.ConnectionString);
        var targetDatabaseName = applicationBuilder.Database;
        if (string.IsNullOrWhiteSpace(targetDatabaseName))
        {
            throw new InvalidOperationException("The connection string does not specify a database name.");
        }

        var maintenanceBuilder = new NpgsqlConnectionStringBuilder(_connectionStringProvider.ConnectionString)
        {
            Database = MaintenanceDatabaseName
        };

        await using var maintenanceConnection = new NpgsqlConnection(maintenanceBuilder.ConnectionString);
        await maintenanceConnection.OpenAsync();

        if (await DatabaseExistsAsync(maintenanceConnection, targetDatabaseName))
        {
            _logService.Information("PostgresDatabaseInitializationService -> EnsureDatabaseExistsAsync", $"database '{targetDatabaseName}' already exists");
            return DatabaseInitializationOutcome.AlreadyExisted;
        }

        var quotedDatabaseName = QuoteIdentifier(targetDatabaseName);

        _logService.Information("PostgresDatabaseInitializationService -> EnsureDatabaseExistsAsync", $"database '{targetDatabaseName}' was not found, creating it");
        await ExecuteNonQueryAsync(maintenanceConnection, $"CREATE DATABASE {quotedDatabaseName} WITH TEMPLATE = template0 ENCODING = 'UTF8'");

        try
        {
            var schemaScript = await ReadSchemaScriptAsync();

            await using var targetConnection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await targetConnection.OpenAsync();
            await ExecuteNonQueryAsync(targetConnection, schemaScript);
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresDatabaseInitializationService -> EnsureDatabaseExistsAsync", $"failed to apply the schema to the new database '{targetDatabaseName}', dropping it", exception);
            await TryDropDatabaseAsync(maintenanceConnection, quotedDatabaseName, targetDatabaseName);
            throw;
        }

        _logService.Information("PostgresDatabaseInitializationService -> EnsureDatabaseExistsAsync", $"database '{targetDatabaseName}' was created and its schema applied");
        return DatabaseInitializationOutcome.Created;
    }

    // Returns true when a database with the given name is listed in the server catalog.
    private static async Task<bool> DatabaseExistsAsync(NpgsqlConnection connection, string databaseName)
    {
        await using var command = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", connection);
        command.Parameters.AddWithValue("name", databaseName);
        var result = await command.ExecuteScalarAsync();
        return result is not null;
    }

    // Executes a statement, or a batch of statements separated by semicolons, that returns no rows.
    private static async Task ExecuteNonQueryAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    // Reads the embedded schema script that creates every table, constraint and index.
    private async Task<string> ReadSchemaScriptAsync()
    {
        var assembly = typeof(PostgresDatabaseInitializationService).Assembly;
        await using var stream = assembly.GetManifestResourceStream(SchemaResourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"The embedded schema script '{SchemaResourceName}' could not be found.");
        }

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    // Attempts to drop a database that was just created but could not be given its schema. A
    // failure to drop is logged as a warning and swallowed, because the original error is what
    // matters to the caller.
    private async Task TryDropDatabaseAsync(NpgsqlConnection maintenanceConnection, string quotedDatabaseName, string databaseName)
    {
        try
        {
            await ExecuteNonQueryAsync(maintenanceConnection, $"DROP DATABASE {quotedDatabaseName}");
            _logService.Information("PostgresDatabaseInitializationService -> TryDropDatabaseAsync", $"dropped the partially created database '{databaseName}'");
        }
        catch (Exception exception)
        {
            _logService.Warning("PostgresDatabaseInitializationService -> TryDropDatabaseAsync", $"failed to drop the partially created database '{databaseName}': {exception.Message}");
        }
    }

    // Wraps an identifier in double quotes, doubling any embedded double quote, so that it can be
    // used safely as a database name in a statement that cannot take a parameter.
    private static string QuoteIdentifier(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"") + "\"";
    }
}
