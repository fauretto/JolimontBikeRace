using JolimontBikeRace.Core.Interfaces;
using JolimontBikeRace.Core.Models;
using JolimontBikeRace.Data.Interfaces;
using Npgsql;

namespace JolimontBikeRace.Data.Repositories;

/// <summary>
/// Provides access to the "race_category" table of the PostgreSQL database, using raw,
/// parameterized SQL statements executed through Npgsql.
/// </summary>
public class PostgresRaceCategoryLinkRepository : IRaceCategoryLinkRepository
{
    private readonly IConnectionStringProvider _connectionStringProvider;
    private readonly ILogService _logService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresRaceCategoryLinkRepository"/> class.
    /// </summary>
    /// <param name="connectionStringProvider">The provider exposing the connection string to use.</param>
    /// <param name="logService">The logging service used to record every failure.</param>
    public PostgresRaceCategoryLinkRepository(IConnectionStringProvider connectionStringProvider, ILogService logService)
    {
        _connectionStringProvider = connectionStringProvider;
        _logService = logService;
    }

    /// <summary>
    /// Retrieves every category link that is currently associated with a given race.
    /// </summary>
    public async Task<IReadOnlyList<RaceCategoryLink>> GetForRaceAsync(long raceIdentifier)
    {
        try
        {
            var links = new List<RaceCategoryLink>();

            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "SELECT idracecategory, idrace, idcategory FROM race_category WHERE idrace = @idrace";
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idrace", raceIdentifier);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                links.Add(new RaceCategoryLink
                {
                    Identifier = reader.GetInt64(reader.GetOrdinal("idracecategory")),
                    RaceIdentifier = reader.GetInt64(reader.GetOrdinal("idrace")),
                    CategoryIdentifier = reader.GetInt64(reader.GetOrdinal("idcategory")),
                });
            }

            return links;
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresRaceCategoryLinkRepository -> GetForRaceAsync", $"failed to retrieve category links for race {raceIdentifier}", exception);
            throw;
        }
    }

    /// <summary>
    /// Associates a category with a race. This operation is idempotent: calling it again for a
    /// pair that is already linked has no additional effect, thanks to the WHERE NOT EXISTS guard
    /// that skips the insertion when the pair is already present. Idempotency is therefore
    /// guaranteed independently of whether the database declares a unique constraint on the
    /// (idrace, idcategory) pair.
    /// </summary>
    public async Task LinkAsync(long raceIdentifier, long categoryIdentifier)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = """
                INSERT INTO race_category (idrace, idcategory)
                SELECT @idrace, @idcategory
                WHERE NOT EXISTS (
                    SELECT 1 FROM race_category WHERE idrace = @idrace AND idcategory = @idcategory
                )
                """;
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idrace", raceIdentifier);
            command.Parameters.AddWithValue("idcategory", categoryIdentifier);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresRaceCategoryLinkRepository -> LinkAsync", $"failed to link category {categoryIdentifier} to race {raceIdentifier}", exception);
            throw;
        }
    }

    /// <summary>
    /// Removes the association between a category and a race.
    /// </summary>
    public async Task UnlinkAsync(long raceIdentifier, long categoryIdentifier)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "DELETE FROM race_category WHERE idrace = @idrace AND idcategory = @idcategory";
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idrace", raceIdentifier);
            command.Parameters.AddWithValue("idcategory", categoryIdentifier);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresRaceCategoryLinkRepository -> UnlinkAsync", $"failed to unlink category {categoryIdentifier} from race {raceIdentifier}", exception);
            throw;
        }
    }
}
