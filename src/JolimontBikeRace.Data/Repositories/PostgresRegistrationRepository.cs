using JolimontBikeRace.Core.Interfaces;
using JolimontBikeRace.Core.Models;
using JolimontBikeRace.Data.Interfaces;
using Npgsql;

namespace JolimontBikeRace.Data.Repositories;

/// <summary>
/// Provides access to the "biker_race_category" table of the PostgreSQL database, using raw,
/// parameterized SQL statements executed through Npgsql.
/// </summary>
public class PostgresRegistrationRepository : IRegistrationRepository
{
    private readonly IConnectionStringProvider _connectionStringProvider;
    private readonly ILogService _logService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresRegistrationRepository"/> class.
    /// </summary>
    /// <param name="connectionStringProvider">The provider exposing the connection string to use.</param>
    /// <param name="logService">The logging service used to record every failure.</param>
    public PostgresRegistrationRepository(IConnectionStringProvider connectionStringProvider, ILogService logService)
    {
        _connectionStringProvider = connectionStringProvider;
        _logService = logService;
    }

    private static Registration MapRow(NpgsqlDataReader reader)
    {
        return new Registration
        {
            Identifier = reader.GetInt64(reader.GetOrdinal("idbikeracecategory")),
            BikerIdentifier = reader.GetInt64(reader.GetOrdinal("idbiker")),
            RaceIdentifier = reader.GetInt64(reader.GetOrdinal("idrace")),
            CategoryIdentifier = reader.IsDBNull(reader.GetOrdinal("idcategory")) ? null : reader.GetInt64(reader.GetOrdinal("idcategory")),
            BibNumber = reader.IsDBNull(reader.GetOrdinal("racenumber")) ? null : reader.GetInt32(reader.GetOrdinal("racenumber")),
        };
    }

    /// <summary>
    /// Retrieves every registration for a given race.
    /// </summary>
    public async Task<IReadOnlyList<Registration>> GetForRaceAsync(long raceIdentifier)
    {
        try
        {
            var registrations = new List<Registration>();

            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "SELECT idbikeracecategory, idbiker, idrace, idcategory, racenumber FROM biker_race_category WHERE idrace = @idrace";
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idrace", raceIdentifier);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                registrations.Add(MapRow(reader));
            }

            return registrations;
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresRegistrationRepository -> GetForRaceAsync", $"failed to retrieve registrations for race {raceIdentifier}", exception);
            throw;
        }
    }

    /// <summary>
    /// Retrieves every registration of a given biker, across every race.
    /// </summary>
    public async Task<IReadOnlyList<Registration>> GetForBikerAsync(long bikerIdentifier)
    {
        try
        {
            var registrations = new List<Registration>();

            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "SELECT idbikeracecategory, idbiker, idrace, idcategory, racenumber FROM biker_race_category WHERE idbiker = @idbiker";
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idbiker", bikerIdentifier);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                registrations.Add(MapRow(reader));
            }

            return registrations;
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresRegistrationRepository -> GetForBikerAsync", $"failed to retrieve registrations for biker {bikerIdentifier}", exception);
            throw;
        }
    }

    /// <summary>
    /// Retrieves the registration matching a given race and bib number, if one exists.
    /// </summary>
    public async Task<Registration?> GetByRaceAndBibNumberAsync(long raceIdentifier, int bibNumber)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "SELECT idbikeracecategory, idbiker, idrace, idcategory, racenumber FROM biker_race_category WHERE idrace = @idrace AND racenumber = @racenumber";
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idrace", raceIdentifier);
            command.Parameters.AddWithValue("racenumber", bibNumber);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapRow(reader);
            }

            return null;
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresRegistrationRepository -> GetByRaceAndBibNumberAsync", $"failed to retrieve registration for race {raceIdentifier} bib {bibNumber}", exception);
            throw;
        }
    }

    /// <summary>
    /// Inserts a new registration into the database.
    /// </summary>
    public async Task<long> AddAsync(Registration registration)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = """
                INSERT INTO biker_race_category (idbiker, idrace, idcategory, racenumber)
                VALUES (@idbiker, @idrace, @idcategory, @racenumber)
                RETURNING idbikeracecategory
                """;
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idbiker", registration.BikerIdentifier);
            command.Parameters.AddWithValue("idrace", registration.RaceIdentifier);
            command.Parameters.AddWithValue("idcategory", (object?)registration.CategoryIdentifier ?? DBNull.Value);
            command.Parameters.AddWithValue("racenumber", (object?)registration.BibNumber ?? DBNull.Value);

            var newIdentifier = (long)(await command.ExecuteScalarAsync())!;
            return newIdentifier;
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresRegistrationRepository -> AddAsync", $"failed to insert registration for biker {registration.BikerIdentifier} in race {registration.RaceIdentifier}", exception);
            throw;
        }
    }

    /// <summary>
    /// Deletes a registration from the database.
    /// </summary>
    public async Task DeleteAsync(long identifier)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "DELETE FROM biker_race_category WHERE idbikeracecategory = @idbikeracecategory";
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idbikeracecategory", identifier);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresRegistrationRepository -> DeleteAsync", $"failed to delete registration {identifier}", exception);
            throw;
        }
    }
}
