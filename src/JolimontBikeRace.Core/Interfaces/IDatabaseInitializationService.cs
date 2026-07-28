namespace JolimontBikeRace.Core.Interfaces;

/// <summary>
/// Describes the result of ensuring that the application database exists at startup.
/// </summary>
public enum DatabaseInitializationOutcome
{
    /// <summary>
    /// The database already existed on the server, so nothing was created.
    /// </summary>
    AlreadyExisted,

    /// <summary>
    /// The database did not exist and was created, together with its schema, during this check.
    /// </summary>
    Created
}

/// <summary>
/// Ensures that the PostgreSQL database used by the application exists before the main window is
/// shown, creating it from the embedded schema script when it is missing.
/// </summary>
public interface IDatabaseInitializationService
{
    /// <summary>
    /// Verifies that the target database exists and, when it does not, creates it and applies the
    /// schema. Throws when the server cannot be reached or the database cannot be created.
    /// </summary>
    /// <returns>The outcome describing whether the database already existed or was created.</returns>
    Task<DatabaseInitializationOutcome> EnsureDatabaseExistsAsync();
}
