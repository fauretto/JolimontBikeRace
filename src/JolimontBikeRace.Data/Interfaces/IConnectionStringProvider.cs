namespace JolimontBikeRace.Data.Interfaces;

/// <summary>
/// Defines an abstraction that exposes the connection string used to reach the PostgreSQL
/// database, so that repositories do not need to know where that connection string comes from.
/// </summary>
public interface IConnectionStringProvider
{
    /// <summary>
    /// Gets the connection string used to open connections to the PostgreSQL database.
    /// </summary>
    string ConnectionString { get; }
}
