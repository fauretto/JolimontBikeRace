using JolimontBikeRace.Core.Models;

namespace JolimontBikeRace.Core.Interfaces;

/// <summary>
/// Defines the operations needed to save and reload the timing data of a race as an XML journal
/// file on disk. The journal acts as an autosave mechanism so that timing data captured during a
/// race is never lost even if the database is momentarily unreachable.
/// </summary>
public interface IRaceStandingsJournalService
{
    /// <summary>
    /// Writes the full list of crossings captured for a race to an XML journal file, in the
    /// historical format used since 2016.
    /// </summary>
    /// <param name="filePath">The full path of the XML file to write.</param>
    /// <param name="race">The race that the crossings belong to.</param>
    /// <param name="crossings">The full list of crossings captured so far for the race.</param>
    /// <param name="bibNumberByBikerIdentifier">
    /// A dictionary mapping each known biker identifier to the bib number that was assigned to
    /// that biker for the race, used to resolve the BIKER_NUMBER value written for each crossing.
    /// </param>
    void WriteJournal(
        string filePath,
        Race race,
        IReadOnlyList<Crossing> crossings,
        IReadOnlyDictionary<long, int> bibNumberByBikerIdentifier);

    /// <summary>
    /// Reads back the crossings and the race start instant previously saved to an XML journal
    /// file.
    /// </summary>
    /// <param name="filePath">The full path of the XML file to read.</param>
    /// <returns>
    /// A tuple containing the list of crossings found in the journal and the start instant of
    /// the race, expressed as .NET ticks.
    /// </returns>
    (IReadOnlyList<Crossing> Crossings, long StartRaceTicks) LoadJournal(string filePath);

    /// <summary>
    /// Writes the start instant of a race to a small, dedicated XML file, in the historical
    /// format used since 2016.
    /// </summary>
    /// <param name="filePath">The full path of the XML file to write.</param>
    /// <param name="race">The race whose start instant must be written.</param>
    void WriteStartDateTime(string filePath, Race race);
}
