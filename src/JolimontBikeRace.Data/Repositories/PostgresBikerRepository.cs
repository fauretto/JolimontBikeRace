using JolimontBikeRace.Core.Interfaces;
using JolimontBikeRace.Core.Models;
using JolimontBikeRace.Data.Interfaces;
using Npgsql;

namespace JolimontBikeRace.Data.Repositories;

/// <summary>
/// Provides access to the "biker" table of the PostgreSQL database, using raw, parameterized SQL
/// statements executed through Npgsql.
/// </summary>
public class PostgresBikerRepository : IBikerRepository
{
    private readonly IConnectionStringProvider _connectionStringProvider;
    private readonly ILogService _logService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresBikerRepository"/> class.
    /// </summary>
    /// <param name="connectionStringProvider">The provider exposing the connection string to use.</param>
    /// <param name="logService">The logging service used to record every failure.</param>
    public PostgresBikerRepository(IConnectionStringProvider connectionStringProvider, ILogService logService)
    {
        _connectionStringProvider = connectionStringProvider;
        _logService = logService;
    }

    private static Biker MapRow(NpgsqlDataReader reader)
    {
        return new Biker
        {
            Identifier = reader.GetInt64(reader.GetOrdinal("idbiker")),
            FirstName = reader.IsDBNull(reader.GetOrdinal("firstname")) ? null : reader.GetString(reader.GetOrdinal("firstname")),
            LastName = reader.IsDBNull(reader.GetOrdinal("lastname")) ? null : reader.GetString(reader.GetOrdinal("lastname")),
            YearOfBirth = reader.IsDBNull(reader.GetOrdinal("yearofbirth")) ? null : reader.GetInt32(reader.GetOrdinal("yearofbirth")),
            Address = reader.IsDBNull(reader.GetOrdinal("address")) ? null : reader.GetString(reader.GetOrdinal("address")),
            ElectronicMailAddress = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString(reader.GetOrdinal("email")),
            Telephone = reader.IsDBNull(reader.GetOrdinal("telephone")) ? null : reader.GetString(reader.GetOrdinal("telephone")),
            MobileTelephone = reader.IsDBNull(reader.GetOrdinal("natel")) ? null : reader.GetString(reader.GetOrdinal("natel")),
        };
    }

    /// <summary>
    /// Retrieves every biker stored in the database.
    /// </summary>
    public async Task<IReadOnlyList<Biker>> GetAllAsync()
    {
        try
        {
            var bikers = new List<Biker>();

            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "SELECT idbiker, firstname, lastname, yearofbirth, address, email, telephone, natel FROM biker ORDER BY lastname, firstname";
            await using var command = new NpgsqlCommand(commandText, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                bikers.Add(MapRow(reader));
            }

            return bikers;
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresBikerRepository -> GetAllAsync", "failed to retrieve the list of bikers", exception);
            throw;
        }
    }

    /// <summary>
    /// Retrieves a single biker by its unique identifier.
    /// </summary>
    public async Task<Biker?> GetByIdentifierAsync(long identifier)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "SELECT idbiker, firstname, lastname, yearofbirth, address, email, telephone, natel FROM biker WHERE idbiker = @idbiker";
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idbiker", identifier);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapRow(reader);
            }

            return null;
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresBikerRepository -> GetByIdentifierAsync", $"failed to retrieve biker {identifier}", exception);
            throw;
        }
    }

    /// <summary>
    /// Inserts a new biker into the database.
    /// </summary>
    public async Task<long> AddAsync(Biker biker)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = """
                INSERT INTO biker (firstname, lastname, yearofbirth, address, email, telephone, natel)
                VALUES (@firstname, @lastname, @yearofbirth, @address, @email, @telephone, @natel)
                RETURNING idbiker
                """;
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("firstname", (object?)biker.FirstName ?? DBNull.Value);
            command.Parameters.AddWithValue("lastname", (object?)biker.LastName ?? DBNull.Value);
            command.Parameters.AddWithValue("yearofbirth", (object?)biker.YearOfBirth ?? DBNull.Value);
            command.Parameters.AddWithValue("address", (object?)biker.Address ?? DBNull.Value);
            command.Parameters.AddWithValue("email", (object?)biker.ElectronicMailAddress ?? DBNull.Value);
            command.Parameters.AddWithValue("telephone", (object?)biker.Telephone ?? DBNull.Value);
            command.Parameters.AddWithValue("natel", (object?)biker.MobileTelephone ?? DBNull.Value);

            var newIdentifier = (long)(await command.ExecuteScalarAsync())!;
            return newIdentifier;
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresBikerRepository -> AddAsync", $"failed to insert biker {biker.FullName}", exception);
            throw;
        }
    }

    /// <summary>
    /// Updates the details of an already existing biker.
    /// </summary>
    public async Task UpdateAsync(Biker biker)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = """
                UPDATE biker
                SET firstname = @firstname, lastname = @lastname, yearofbirth = @yearofbirth,
                    address = @address, email = @email, telephone = @telephone, natel = @natel
                WHERE idbiker = @idbiker
                """;
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idbiker", biker.Identifier);
            command.Parameters.AddWithValue("firstname", (object?)biker.FirstName ?? DBNull.Value);
            command.Parameters.AddWithValue("lastname", (object?)biker.LastName ?? DBNull.Value);
            command.Parameters.AddWithValue("yearofbirth", (object?)biker.YearOfBirth ?? DBNull.Value);
            command.Parameters.AddWithValue("address", (object?)biker.Address ?? DBNull.Value);
            command.Parameters.AddWithValue("email", (object?)biker.ElectronicMailAddress ?? DBNull.Value);
            command.Parameters.AddWithValue("telephone", (object?)biker.Telephone ?? DBNull.Value);
            command.Parameters.AddWithValue("natel", (object?)biker.MobileTelephone ?? DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresBikerRepository -> UpdateAsync", $"failed to update biker {biker.Identifier}", exception);
            throw;
        }
    }

    /// <summary>
    /// Deletes a biker from the database.
    /// </summary>
    public async Task DeleteAsync(long identifier)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "DELETE FROM biker WHERE idbiker = @idbiker";
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idbiker", identifier);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresBikerRepository -> DeleteAsync", $"failed to delete biker {identifier}", exception);
            throw;
        }
    }
}
