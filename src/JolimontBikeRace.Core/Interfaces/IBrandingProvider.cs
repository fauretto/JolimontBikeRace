namespace JolimontBikeRace.Core.Interfaces;

/// <summary>
/// Provides the customer-configurable name shown across the application, allowing the brand
/// displayed in the window title, the top header and printed documents to be customized without
/// recompiling the application.
/// </summary>
public interface IBrandingProvider
{
    /// <summary>
    /// Gets the display name read from the branding file, or a built-in default when the file is
    /// missing or invalid.
    /// </summary>
    string RaceName { get; }
}
