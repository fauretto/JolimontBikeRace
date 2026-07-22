using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using JolimontBikeRace.Core.Interfaces;
using JolimontBikeRace.Core.Models;

namespace JolimontBikeRace.Data.Services;

/// <summary>
/// Reads and writes the timing data of a race as an XML journal file on disk, reproducing the
/// exact, byte-compatible textual format that has been used by the application since the 2016
/// edition of the race, so that historical journal files remain fully compatible.
/// </summary>
public class XmlRaceStandingsJournalService : IRaceStandingsJournalService
{
    private readonly ILogService _logService;

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlRaceStandingsJournalService"/> class.
    /// </summary>
    /// <param name="logService">The logging service used to record every write and any failure.</param>
    public XmlRaceStandingsJournalService(ILogService logService)
    {
        _logService = logService;
    }

    // Common writer settings shared by every method of this class: no automatic XML declaration
    // (a hand-written, encoding-free declaration is emitted instead so that the output matches
    // the historical format exactly), no indentation and no injected line breaks, so that the
    // whole document is written as a single line, exactly like the historical journal files.
    private static XmlWriterSettings CreateWriterSettings()
    {
        return new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = false,
            NewLineChars = string.Empty,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
    }

    /// <summary>
    /// Writes the full list of crossings captured for a race to an XML journal file, in the
    /// historical format used since 2016.
    /// </summary>
    public void WriteJournal(
        string filePath,
        Race race,
        IReadOnlyList<Crossing> crossings,
        IReadOnlyDictionary<long, int> bibNumberByBikerIdentifier)
    {
        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            using var writer = XmlWriter.Create(fileStream, CreateWriterSettings());

            writer.WriteRaw("<?xml version=\"1.0\"?>");
            writer.WriteComment("Jolimont Bike Race Standings");
            writer.WriteStartElement("RACE_STANDINGS");
            writer.WriteAttributeString("StartRaceTick", race.StartTicks.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("RaceName", race.Name);

            foreach (var crossing in crossings)
            {
                // The bib number is not stored on the crossing itself: it is resolved through the
                // supplied dictionary, and defaults to zero when the biker has not been assigned
                // a bib number yet.
                var bibNumber = bibNumberByBikerIdentifier.GetValueOrDefault(crossing.BikerIdentifier, 0);

                writer.WriteStartElement("RACE_STANDING");
                writer.WriteElementString("TICK_INDEX", crossing.SequenceIndex.ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("RACE_TICKS", crossing.Ticks.ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("BIKER_NUMBER", bibNumber.ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("BIKER_ID", crossing.BikerIdentifier.ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("RACE_ID", crossing.RaceIdentifier.ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.Flush();

            _logService.Information(
                "XmlRaceStandingsJournalService -> WriteJournal",
                $"journal saved to {filePath}");
        }
        catch (Exception exception)
        {
            _logService.Error(
                "XmlRaceStandingsJournalService -> WriteJournal",
                $"failed to save journal to {filePath}",
                exception);
            throw;
        }
    }

    /// <summary>
    /// Reads back the crossings and the race start instant previously saved to an XML journal
    /// file.
    /// </summary>
    public (IReadOnlyList<Crossing> Crossings, long StartRaceTicks) LoadJournal(string filePath)
    {
        try
        {
            var document = XDocument.Load(filePath);
            var rootElement = document.Root
                ?? throw new InvalidDataException($"The journal file '{filePath}' does not contain a root element.");

            var startRaceTicks = long.Parse(
                rootElement.Attribute("StartRaceTick")?.Value ?? "0",
                CultureInfo.InvariantCulture);

            var crossings = rootElement.Elements("RACE_STANDING")
                .Select(element => new Crossing
                {
                    SequenceIndex = long.Parse(element.Element("TICK_INDEX")!.Value, CultureInfo.InvariantCulture),
                    Ticks = long.Parse(element.Element("RACE_TICKS")!.Value, CultureInfo.InvariantCulture),
                    BikerIdentifier = long.Parse(element.Element("BIKER_ID")!.Value, CultureInfo.InvariantCulture),
                    RaceIdentifier = long.Parse(element.Element("RACE_ID")!.Value, CultureInfo.InvariantCulture),
                })
                .ToList();

            _logService.Information(
                "XmlRaceStandingsJournalService -> LoadJournal",
                $"journal loaded from {filePath} with {crossings.Count} crossings");

            return (crossings, startRaceTicks);
        }
        catch (Exception exception)
        {
            _logService.Error(
                "XmlRaceStandingsJournalService -> LoadJournal",
                $"failed to load journal from {filePath}",
                exception);
            throw;
        }
    }

    /// <summary>
    /// Writes the start instant of a race to a small, dedicated XML file, in the historical
    /// format used since 2016.
    /// </summary>
    public void WriteStartDateTime(string filePath, Race race)
    {
        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            using var writer = XmlWriter.Create(fileStream, CreateWriterSettings());

            var startDateTime = new DateTime(race.StartTicks);

            writer.WriteRaw("<?xml version=\"1.0\"?>");
            writer.WriteComment("Jolimont Bike Race Timing");
            writer.WriteStartElement("RACE_DATETIME");
            writer.WriteElementString("START_DATE_TIME", startDateTime.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture));
            writer.WriteElementString("START_TICKS", race.StartTicks.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("RACE_ID", race.Identifier.ToString(CultureInfo.InvariantCulture));

            // The historical format always writes an empty RACE_NAME element, regardless of the
            // actual name of the race, so the same convention is reproduced here.
            writer.WriteStartElement("RACE_NAME");
            writer.WriteEndElement();

            writer.WriteEndElement();
            writer.Flush();

            _logService.Information(
                "XmlRaceStandingsJournalService -> WriteStartDateTime",
                $"start date and time saved to {filePath}");
        }
        catch (Exception exception)
        {
            _logService.Error(
                "XmlRaceStandingsJournalService -> WriteStartDateTime",
                $"failed to save start date and time to {filePath}",
                exception);
            throw;
        }
    }
}
