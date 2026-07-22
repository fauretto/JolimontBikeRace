using JolimontBikeRace.Core.Interfaces;
using JolimontBikeRace.Core.Models;
using JolimontBikeRace.Data.Interfaces;
using Npgsql;

namespace JolimontBikeRace.Data.Repositories;

/// <summary>
/// Provides access to the "race" table of the PostgreSQL database, using raw, parameterized SQL
/// statements executed through Npgsql.
/// </summary>
public class PostgresRaceRepository : IRaceRepository
{
    private readonly IConnectionStringProvider _connectionStringProvider;
    private readonly ILogService _logService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresRaceRepository"/> class.
    /// </summary>
    /// <param name="connectionStringProvider">The provider exposing the connection string to use.</param>
    /// <param name="logService">The logging service used to record every failure and every race start.</param>
    public PostgresRaceRepository(IConnectionStringProvider connectionStringProvider, ILogService logService)
    {
        _connectionStringProvider = connectionStringProvider;
        _logService = logService;
    }

    private static Race MapRow(NpgsqlDataReader reader)
    {
        return new Race
        {
            Identifier = reader.GetInt64(reader.GetOrdinal("idrace")),
            Name = reader.GetString(reader.GetOrdinal("racename")),
            StartTicks = reader.GetInt64(reader.GetOrdinal("racetick")),
        };
    }

    /// <summary>
    /// Retrieves every race stored in the database.
    /// </summary>
    public async Task<IReadOnlyList<Race>> GetAllAsync()
    {
        try
        {
            var races = new List<Race>();

            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "SELECT idrace, racename, racetick FROM race ORDER BY racename";
            await using var command = new NpgsqlCommand(commandText, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                races.Add(MapRow(reader));
            }

            return races;
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresRaceRepository -> GetAllAsync", "failed to retrieve the list of races", exception);
            throw;
        }
    }

    /// <summary>
    /// Retrieves a single race by its unique identifier.
    /// </summary>
    public async Task<Race?> GetByIdentifierAsync(long identifier)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "SELECT idrace, racename, racetick FROM race WHERE idrace = @idrace";
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idrace", identifier);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapRow(reader);
            }

            return null;
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresRaceRepository -> GetByIdentifierAsync", $"failed to retrieve race {identifier}", exception);
            throw;
        }
    }

    /// <summary>
    /// Inserts a new race into the database.
    /// </summary>
    public async Task<long> AddAsync(Race race)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "INSERT INTO race (racename, racetick) VALUES (@racename, @racetick) RETURNING idrace";
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("racename", race.Name);
            command.Parameters.AddWithValue("racetick", race.StartTicks);

            var newIdentifier = (long)(await command.ExecuteScalarAsync())!;
            return newIdentifier;
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresRaceRepository -> AddAsync", $"failed to insert race {race.Name}", exception);
            throw;
        }
    }

    /// <summary>
    /// Updates the details of an already existing race.
    /// </summary>
    public async Task UpdateAsync(Race race)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "UPDATE race SET racename = @racename, racetick = @racetick WHERE idrace = @idrace";
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idrace", race.Identifier);
            command.Parameters.AddWithValue("racename", race.Name);
            command.Parameters.AddWithValue("racetick", race.StartTicks);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresRaceRepository -> UpdateAsync", $"failed to update race {race.Identifier}", exception);
            throw;
        }
    }

    /// <summary>
    /// Deletes a race from the database.
    /// </summary>
    public async Task DeleteAsync(long identifier)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "DELETE FROM race WHERE idrace = @idrace";
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idrace", identifier);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresRaceRepository -> DeleteAsync", $"failed to delete race {identifier}", exception);
            throw;
        }
    }

    /// <summary>
    /// Records the start instant of a race. This is a critical operation because it marks the
    /// exact moment from which every rider's elapsed race time will be computed, so its outcome
    /// is always logged.
    /// </summary>
    public async Task UpdateStartTicksAsync(long raceIdentifier, long startTicks)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "UPDATE race SET racetick = @racetick WHERE idrace = @idrace";
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idrace", raceIdentifier);
            command.Parameters.AddWithValue("racetick", startTicks);

            await command.ExecuteNonQueryAsync();

            // This is the critical section that records the official start instant of the race:
            // every elapsed race time will be computed relative to this value, so it must always
            // be logged.
            _logService.Information(
                "PostgresRaceRepository -> UpdateStartTicksAsync",
                $"race {raceIdentifier} started at tick {startTicks}");
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresRaceRepository -> UpdateStartTicksAsync", $"failed to record the start instant of race {raceIdentifier}", exception);
            throw;
        }
    }
}
