using JolimontBikeRace.Data.Interfaces;

namespace JolimontBikeRace.Data;

/// <summary>
/// Provides the connection string used to reach the PostgreSQL database, as a simple immutable
/// wrapper around a string supplied at construction time, typically read from the application
/// configuration.
/// </summary>
public class ConnectionStringProvider : IConnectionStringProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionStringProvider"/> class.
    /// </summary>
    /// <param name="connectionString">The connection string to expose through this provider.</param>
    public ConnectionStringProvider(string connectionString)
    {
        ConnectionString = connectionString;
    }

    /// <summary>
    /// Gets the connection string used to open connections to the PostgreSQL database.
    /// </summary>
    public string ConnectionString { get; }
}
