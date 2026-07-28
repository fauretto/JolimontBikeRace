using System.IO;
using System.Xml.Linq;
using JolimontBikeRace.Core.Interfaces;

namespace JolimontBikeRace.App.Services;

/// <summary>
/// Implements <see cref="IBrandingProvider"/> by reading the customer-configurable race name once,
/// at construction time, from an extensible markup language file deployed next to the executable,
/// falling back to a built-in default whenever that file is missing, malformed, or unreadable.
/// </summary>
public class XmlBrandingProvider : IBrandingProvider
{
    /// <summary>
    /// The built-in race name used whenever the branding file cannot be read or does not contain a
    /// usable value.
    /// </summary>
    public const string DefaultRaceName = "Jolimont Bike Race";

    private readonly string _brandingFilePath;
    private readonly ILogService _logService;

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlBrandingProvider"/> class, immediately
    /// reading and caching the race name for the lifetime of this instance.
    /// </summary>
    /// <param name="brandingFilePath">The full path of the branding extensible markup language file deployed next to the executable.</param>
    /// <param name="logService">The logging service used to record how the race name was resolved.</param>
    public XmlBrandingProvider(string brandingFilePath, ILogService logService)
    {
        _brandingFilePath = brandingFilePath;
        _logService = logService;

        RaceName = LoadRaceName();
    }

    /// <summary>
    /// Gets the display name read from the branding file, or a built-in default when the file is
    /// missing or invalid.
    /// </summary>
    public string RaceName { get; }

    private string LoadRaceName()
    {
        if (!File.Exists(_brandingFilePath))
        {
            _logService.Warning("XmlBrandingProvider -> LoadRaceName", $"branding file not found at {_brandingFilePath}, using the default race name");
            return DefaultRaceName;
        }

        try
        {
            var document = XDocument.Load(_brandingFilePath);
            var raceName = document.Root?.Element("RaceName")?.Value?.Trim();

            if (string.IsNullOrWhiteSpace(raceName))
            {
                _logService.Warning("XmlBrandingProvider -> LoadRaceName", $"the RaceName element is missing or empty in {_brandingFilePath}, using the default race name");
                return DefaultRaceName;
            }

            _logService.Information("XmlBrandingProvider -> LoadRaceName", $"loaded race name '{raceName}' from {_brandingFilePath}");
            return raceName;
        }
        catch (Exception exception)
        {
            _logService.Error("XmlBrandingProvider -> LoadRaceName", "failed to read the branding file, using the default race name", exception);
            return DefaultRaceName;
        }
    }
}
