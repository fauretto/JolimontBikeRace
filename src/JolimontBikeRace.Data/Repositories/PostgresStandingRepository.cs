using JolimontBikeRace.Core.Interfaces;
using JolimontBikeRace.Core.Models;
using JolimontBikeRace.Data.Interfaces;
using Npgsql;

namespace JolimontBikeRace.Data.Repositories;

/// <summary>
/// Provides access to the "standing" table of the PostgreSQL database, using raw, parameterized
/// SQL statements executed through Npgsql.
/// </summary>
public class PostgresStandingRepository : IStandingRepository
{
    private readonly IConnectionStringProvider _connectionStringProvider;
    private readonly ILogService _logService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresStandingRepository"/> class.
    /// </summary>
    /// <param name="connectionStringProvider">The provider exposing the connection string to use.</param>
    /// <param name="logService">The logging service used to record every failure and every save of official results.</param>
    public PostgresStandingRepository(IConnectionStringProvider connectionStringProvider, ILogService logService)
    {
        _connectionStringProvider = connectionStringProvider;
        _logService = logService;
    }

    /// <summary>
    /// Retrieves the computed final classification stored for a given race.
    /// </summary>
    public async Task<IReadOnlyList<StandingEntry>> GetForRaceAsync(long raceIdentifier)
    {
        try
        {
            var entries = new List<StandingEntry>();

            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "SELECT idstanding, idbiker, idrace, raceposition, tick, racetime, gap FROM standing WHERE idrace = @idrace ORDER BY raceposition";
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idrace", raceIdentifier);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                entries.Add(new StandingEntry
                {
                    Identifier = reader.GetInt64(reader.GetOrdinal("idstanding")),
                    BikerIdentifier = reader.GetInt64(reader.GetOrdinal("idbiker")),
                    RaceIdentifier = reader.GetInt64(reader.GetOrdinal("idrace")),
                    Position = reader.GetInt32(reader.GetOrdinal("raceposition")),
                    Ticks = reader.GetInt64(reader.GetOrdinal("tick")),
                    RaceTime = reader.IsDBNull(reader.GetOrdinal("racetime")) ? null : reader.GetString(reader.GetOrdinal("racetime")),
                    Gap = reader.IsDBNull(reader.GetOrdinal("gap")) ? null : reader.GetString(reader.GetOrdinal("gap")),
                });
            }

            return entries;
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresStandingRepository -> GetForRaceAsync", $"failed to retrieve standings for race {raceIdentifier}", exception);
            throw;
        }
    }

    /// <summary>
    /// Replaces the computed final classification stored for a given race with a new set of
    /// entries, as a single atomic transaction. This is the critical section that commits the
    /// official results of the race to the database, so the number of rows written is always
    /// logged.
    /// </summary>
    public async Task ReplaceForRaceAsync(long raceIdentifier, IReadOnlyList<StandingEntry> entries)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            await using var transaction = await connection.BeginTransactionAsync();

            const string deleteCommandText = "DELETE FROM standing WHERE idrace = @idrace";
            await using (var deleteCommand = new NpgsqlCommand(deleteCommandText, connection, transaction))
            {
                deleteCommand.Parameters.AddWithValue("idrace", raceIdentifier);
                await deleteCommand.ExecuteNonQueryAsync();
            }

            const string insertCommandText = """
                INSERT INTO standing (idbiker, idrace, raceposition, tick, racetime, gap)
                VALUES (@idbiker, @idrace, @raceposition, @tick, @racetime, @gap)
                """;
            foreach (var entry in entries)
            {
                await using var insertCommand = new NpgsqlCommand(insertCommandText, connection, transaction);
                insertCommand.Parameters.AddWithValue("idbiker", entry.BikerIdentifier);
                insertCommand.Parameters.AddWithValue("idrace", raceIdentifier);
                insertCommand.Parameters.AddWithValue("raceposition", entry.Position);
                insertCommand.Parameters.AddWithValue("tick", entry.Ticks);
                insertCommand.Parameters.AddWithValue("racetime", (object?)entry.RaceTime ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("gap", (object?)entry.Gap ?? DBNull.Value);
                await insertCommand.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            // This is the critical section that commits the official results of the race to the
            // database: its outcome must always be logged with the number of rows written.
            _logService.Information(
                "PostgresStandingRepository -> ReplaceForRaceAsync",
                $"official results saved: {entries.Count} standings written for race {raceIdentifier}");
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresStandingRepository -> ReplaceForRaceAsync", $"failed to save official results for race {raceIdentifier}", exception);
            throw;
        }
    }
}
