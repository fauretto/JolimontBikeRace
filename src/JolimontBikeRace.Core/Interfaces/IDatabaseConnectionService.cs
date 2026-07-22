namespace JolimontBikeRace.Core.Interfaces;

/// <summary>
/// Defines the operations needed to verify and describe the connectivity to the PostgreSQL
/// database used by the application.
/// </summary>
public interface IDatabaseConnectionService
{
    /// <summary>
    /// Attempts to open a connection to the database and to execute a trivial query, in order to
    /// verify that the database is reachable and correctly configured.
    /// </summary>
    /// <returns>True when the connection succeeded, false otherwise.</returns>
    Task<bool> TestConnectionAsync();

    /// <summary>
    /// Gets the name of the database that this service connects to, used for display purposes in
    /// the user interface.
    /// </summary>
    string DatabaseName { get; }
}
