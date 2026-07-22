using JolimontBikeRace.Core.Interfaces;
using JolimontBikeRace.Core.Models;
using JolimontBikeRace.Data.Interfaces;
using Npgsql;

namespace JolimontBikeRace.Data.Repositories;

/// <summary>
/// Provides access to the "race_standings" table of the PostgreSQL database, using raw,
/// parameterized SQL statements executed through Npgsql.
/// </summary>
public class PostgresCrossingRepository : ICrossingRepository
{
    private readonly IConnectionStringProvider _connectionStringProvider;
    private readonly ILogService _logService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresCrossingRepository"/> class.
    /// </summary>
    /// <param name="connectionStringProvider">The provider exposing the connection string to use.</param>
    /// <param name="logService">The logging service used to record every failure and every bulk commit.</param>
    public PostgresCrossingRepository(IConnectionStringProvider connectionStringProvider, ILogService logService)
    {
        _connectionStringProvider = connectionStringProvider;
        _logService = logService;
    }

    private static Crossing MapRow(NpgsqlDataReader reader)
    {
        return new Crossing
        {
            Identifier = reader.GetInt64(reader.GetOrdinal("idstanding")),
            BikerIdentifier = reader.GetInt64(reader.GetOrdinal("idbiker")),
            RaceIdentifier = reader.GetInt64(reader.GetOrdinal("idrace")),
            SequenceIndex = reader.GetInt64(reader.GetOrdinal("tickindex")),
            Ticks = reader.GetInt64(reader.GetOrdinal("tick")),
        };
    }

    /// <summary>
    /// Retrieves every crossing captured for a given race, ordered by sequence index.
    /// </summary>
    public async Task<IReadOnlyList<Crossing>> GetForRaceAsync(long raceIdentifier)
    {
        try
        {
            var crossings = new List<Crossing>();

            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "SELECT idstanding, idbiker, idrace, tickindex, tick FROM race_standings WHERE idrace = @idrace ORDER BY tickindex";
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idrace", raceIdentifier);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                crossings.Add(MapRow(reader));
            }

            return crossings;
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresCrossingRepository -> GetForRaceAsync", $"failed to retrieve crossings for race {raceIdentifier}", exception);
            throw;
        }
    }

    /// <summary>
    /// Inserts a new crossing into the database.
    /// </summary>
    public async Task<long> AddAsync(Crossing crossing)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = """
                INSERT INTO race_standings (idbiker, idrace, tickindex, tick)
                VALUES (@idbiker, @idrace, @tickindex, @tick)
                RETURNING idstanding
                """;
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idbiker", crossing.BikerIdentifier);
            command.Parameters.AddWithValue("idrace", crossing.RaceIdentifier);
            command.Parameters.AddWithValue("tickindex", crossing.SequenceIndex);
            command.Parameters.AddWithValue("tick", crossing.Ticks);

            var newIdentifier = (long)(await command.ExecuteScalarAsync())!;
            return newIdentifier;
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresCrossingRepository -> AddAsync", $"failed to insert crossing for race {crossing.RaceIdentifier}", exception);
            throw;
        }
    }

    /// <summary>
    /// Updates the details of an already existing crossing, for example to assign a bib number
    /// that was unknown at capture time.
    /// </summary>
    public async Task UpdateAsync(Crossing crossing)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "UPDATE race_standings SET idbiker = @idbiker, tickindex = @tickindex, tick = @tick WHERE idstanding = @idstanding";
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idstanding", crossing.Identifier);
            command.Parameters.AddWithValue("idbiker", crossing.BikerIdentifier);
            command.Parameters.AddWithValue("tickindex", crossing.SequenceIndex);
            command.Parameters.AddWithValue("tick", crossing.Ticks);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresCrossingRepository -> UpdateAsync", $"failed to update crossing {crossing.Identifier}", exception);
            throw;
        }
    }

    /// <summary>
    /// Deletes a single crossing from the database.
    /// </summary>
    public async Task DeleteAsync(long identifier)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "DELETE FROM race_standings WHERE idstanding = @idstanding";
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idstanding", identifier);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresCrossingRepository -> DeleteAsync", $"failed to delete crossing {identifier}", exception);
            throw;
        }
    }

    /// <summary>
    /// Deletes every crossing recorded for a given race. This operation is typically used when
    /// resetting a race before it starts again.
    /// </summary>
    public async Task DeleteAllForRaceAsync(long raceIdentifier)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "DELETE FROM race_standings WHERE idrace = @idrace";
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idrace", raceIdentifier);

            var rowsDeleted = await command.ExecuteNonQueryAsync();

            _logService.Information(
                "PostgresCrossingRepository -> DeleteAllForRaceAsync",
                $"deleted {rowsDeleted} crossings for race {raceIdentifier}");
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresCrossingRepository -> DeleteAllForRaceAsync", $"failed to delete all crossings for race {raceIdentifier}", exception);
            throw;
        }
    }

    /// <summary>
    /// Replaces every crossing recorded for a given race with a new set of crossings, as a
    /// single atomic transaction. This is the critical section that commits the timing data
    /// captured for the whole race to the database, so the number of rows written is always
    /// logged.
    /// </summary>
    public async Task ReplaceAllForRaceAsync(long raceIdentifier, IReadOnlyList<Crossing> crossings)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            await using var transaction = await connection.BeginTransactionAsync();

            const string deleteCommandText = "DELETE FROM race_standings WHERE idrace = @idrace";
            await using (var deleteCommand = new NpgsqlCommand(deleteCommandText, connection, transaction))
            {
                deleteCommand.Parameters.AddWithValue("idrace", raceIdentifier);
                await deleteCommand.ExecuteNonQueryAsync();
            }

            const string insertCommandText = """
                INSERT INTO race_standings (idbiker, idrace, tickindex, tick)
                VALUES (@idbiker, @idrace, @tickindex, @tick)
                """;
            foreach (var crossing in crossings)
            {
                await using var insertCommand = new NpgsqlCommand(insertCommandText, connection, transaction);
                insertCommand.Parameters.AddWithValue("idbiker", crossing.BikerIdentifier);
                insertCommand.Parameters.AddWithValue("idrace", raceIdentifier);
                insertCommand.Parameters.AddWithValue("tickindex", crossing.SequenceIndex);
                insertCommand.Parameters.AddWithValue("tick", crossing.Ticks);
                await insertCommand.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            // This is the critical section that commits the entire timing data of the race to
            // the database: its outcome must always be logged with the number of rows written.
            _logService.Information(
                "PostgresCrossingRepository -> ReplaceAllForRaceAsync",
                $"committed {crossings.Count} crossings to database for race {raceIdentifier}");
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresCrossingRepository -> ReplaceAllForRaceAsync", $"failed to replace crossings for race {raceIdentifier}", exception);
            throw;
        }
    }
}
