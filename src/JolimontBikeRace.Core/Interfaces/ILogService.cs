namespace JolimontBikeRace.Core.Interfaces;

/// <summary>
/// Defines a small abstraction over the logging infrastructure, so that the rest of the
/// application does not depend directly on a specific logging library. Implementations are
/// expected to write every message following the convention "ClassName -> MethodName : message".
/// </summary>
public interface ILogService
{
    /// <summary>
    /// Records an informational message describing a normal event of the application, for
    /// example a successful save or a completed startup step.
    /// </summary>
    /// <param name="source">The "ClassName -> MethodName" prefix identifying where the message originates from.</param>
    /// <param name="message">The message describing the event that occurred.</param>
    void Information(string source, string message);

    /// <summary>
    /// Records a warning message describing an event that is not an error but that deserves
    /// attention, for example a rejected validation or an unassigned crossing.
    /// </summary>
    /// <param name="source">The "ClassName -> MethodName" prefix identifying where the message originates from.</param>
    /// <param name="message">The message describing the event that occurred.</param>
    void Warning(string source, string message);

    /// <summary>
    /// Records an error message describing a failure that occurred while executing the
    /// application, without an associated exception instance.
    /// </summary>
    /// <param name="source">The "ClassName -> MethodName" prefix identifying where the message originates from.</param>
    /// <param name="message">The message describing the failure that occurred.</param>
    void Error(string source, string message);

    /// <summary>
    /// Records an error message describing a failure that occurred while executing the
    /// application, together with the exception instance that was raised.
    /// </summary>
    /// <param name="source">The "ClassName -> MethodName" prefix identifying where the message originates from.</param>
    /// <param name="message">The message describing the failure that occurred.</param>
    /// <param name="exception">The exception instance that was caught while the failure occurred.</param>
    void Error(string source, string message, Exception exception);
}
