using System.IO;
using JolimontBikeRace.Core.Interfaces;
using log4net;
using log4net.Config;

namespace JolimontBikeRace.App.Services;

/// <summary>
/// Implements <see cref="ILogService"/> on top of log4net, writing every message through a
/// single named logger so that all of the application's log output ends up in the same rolling
/// file, following the "ClassName -> MethodName : message" convention for the source prefix.
/// </summary>
public class Log4NetLogService : ILogService
{
    private static readonly ILog Logger = LogManager.GetLogger("JolimontBikeRace");

    /// <summary>
    /// Configures log4net from the log4net.config file deployed next to the executable, and
    /// ensures that the LOG subfolder used by the rolling file appender exists before the first
    /// log message is written.
    /// </summary>
    public static void Configure()
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var logDirectory = Path.Combine(baseDirectory, "LOG");
        Directory.CreateDirectory(logDirectory);

        var configurationFile = new FileInfo(Path.Combine(baseDirectory, "log4net.config"));
        XmlConfigurator.Configure(configurationFile);
    }

    /// <summary>
    /// Records an informational message describing a normal event of the application.
    /// </summary>
    public void Information(string source, string message)
    {
        Logger.Info($"[{source}] {message}");
    }

    /// <summary>
    /// Records a warning message describing an event that is not an error but that deserves
    /// attention.
    /// </summary>
    public void Warning(string source, string message)
    {
        Logger.Warn($"[{source}] {message}");
    }

    /// <summary>
    /// Records an error message describing a failure that occurred while executing the
    /// application, without an associated exception instance.
    /// </summary>
    public void Error(string source, string message)
    {
        Logger.Error($"[{source}] {message}");
    }

    /// <summary>
    /// Records an error message describing a failure that occurred while executing the
    /// application, together with the exception instance that was raised.
    /// </summary>
    public void Error(string source, string message, Exception exception)
    {
        Logger.Error($"[{source}] {message}", exception);
    }
}
