using JolimontBikeRace.Core.Interfaces;
using JolimontBikeRace.Core.Models;
using JolimontBikeRace.Data.Interfaces;
using Npgsql;

namespace JolimontBikeRace.Data.Repositories;

/// <summary>
/// Provides access to the "category" table of the PostgreSQL database, using raw, parameterized
/// SQL statements executed through Npgsql.
/// </summary>
public class PostgresCategoryRepository : ICategoryRepository
{
    private readonly IConnectionStringProvider _connectionStringProvider;
    private readonly ILogService _logService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresCategoryRepository"/> class.
    /// </summary>
    /// <param name="connectionStringProvider">The provider exposing the connection string to use.</param>
    /// <param name="logService">The logging service used to record every failure.</param>
    public PostgresCategoryRepository(IConnectionStringProvider connectionStringProvider, ILogService logService)
    {
        _connectionStringProvider = connectionStringProvider;
        _logService = logService;
    }

    private static Category MapRow(NpgsqlDataReader reader)
    {
        return new Category
        {
            Identifier = reader.GetInt64(reader.GetOrdinal("idcategory")),
            Name = reader.GetString(reader.GetOrdinal("categoryname")),
            MinimumBibNumber = reader.GetInt32(reader.GetOrdinal("minnumber")),
            MaximumBibNumber = reader.GetInt32(reader.GetOrdinal("maxnumber")),
        };
    }

    /// <summary>
    /// Retrieves every category stored in the database.
    /// </summary>
    public async Task<IReadOnlyList<Category>> GetAllAsync()
    {
        try
        {
            var categories = new List<Category>();

            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "SELECT idcategory, categoryname, minnumber, maxnumber FROM category ORDER BY categoryname";
            await using var command = new NpgsqlCommand(commandText, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(MapRow(reader));
            }

            return categories;
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresCategoryRepository -> GetAllAsync", "failed to retrieve the list of categories", exception);
            throw;
        }
    }

    /// <summary>
    /// Inserts a new category into the database.
    /// </summary>
    public async Task<long> AddAsync(Category category)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "INSERT INTO category (categoryname, minnumber, maxnumber) VALUES (@categoryname, @minnumber, @maxnumber) RETURNING idcategory";
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("categoryname", category.Name);
            command.Parameters.AddWithValue("minnumber", category.MinimumBibNumber);
            command.Parameters.AddWithValue("maxnumber", category.MaximumBibNumber);

            var newIdentifier = (long)(await command.ExecuteScalarAsync())!;
            return newIdentifier;
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresCategoryRepository -> AddAsync", $"failed to insert category {category.Name}", exception);
            throw;
        }
    }

    /// <summary>
    /// Updates the details of an already existing category.
    /// </summary>
    public async Task UpdateAsync(Category category)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "UPDATE category SET categoryname = @categoryname, minnumber = @minnumber, maxnumber = @maxnumber WHERE idcategory = @idcategory";
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idcategory", category.Identifier);
            command.Parameters.AddWithValue("categoryname", category.Name);
            command.Parameters.AddWithValue("minnumber", category.MinimumBibNumber);
            command.Parameters.AddWithValue("maxnumber", category.MaximumBibNumber);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresCategoryRepository -> UpdateAsync", $"failed to update category {category.Identifier}", exception);
            throw;
        }
    }

    /// <summary>
    /// Deletes a category from the database.
    /// </summary>
    public async Task DeleteAsync(long identifier)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringProvider.ConnectionString);
            await connection.OpenAsync();

            const string commandText = "DELETE FROM category WHERE idcategory = @idcategory";
            await using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("idcategory", identifier);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception)
        {
            _logService.Error("PostgresCategoryRepository -> DeleteAsync", $"failed to delete category {identifier}", exception);
            throw;
        }
    }
}
