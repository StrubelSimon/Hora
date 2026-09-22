using Hora.Core.Asana;
using Hora.Core.Asana.Models;

namespace Hora.Core.Worklogs;

/// <summary>
/// Der Asana-Anbieter hinter <see cref="IWorklogService"/>. Holt die Zeiteinträge eines Nutzers in
/// einem Workspace und bildet sie auf <see cref="WorklogEntry"/> ab.
/// </summary>
/// <remarks>
/// Asana-Einträge kennen nur einen Tag, keine Uhrzeit. Der Zeitstempel wird deshalb auf lokale
/// Mitternacht des gebuchten Tags gesetzt: Damit fällt der Eintrag beim Aggregieren auf genau den
/// Kalendertag, den Asana meint, und Tages-, Wochen- und Monatssummen laufen ohne Sonderzweig.
/// </remarks>
public sealed class AsanaWorklogService : IWorklogService
{
    /// <summary>Ersatzkennung für einen Eintrag ohne zugerechnetes Projekt.</summary>
    private const string FallbackKey = "Asana";

    private readonly IAsanaClient _client;
    private readonly string _userGid;
    private readonly string _workspaceGid;
    private readonly TimeZoneInfo _zone;

    public AsanaWorklogService(IAsanaClient client, string userGid, string workspaceGid, TimeZoneInfo zone)
    {
        _client = client;
        _userGid = userGid;
        _workspaceGid = workspaceGid;
        _zone = zone;
    }

    public async Task<WorklogFetchResult> GetEntriesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from)
        {
            throw new ArgumentException("Das Enddatum liegt vor dem Startdatum.", nameof(to));
        }

        var timeEntries = await _client.GetTimeEntriesAsync(_userGid, _workspaceGid, from, to, cancellationToken);

        var entries = timeEntries
            // Ein Eintrag ohne Dauer trägt nichts zur Summe bei und erzeugte nur eine leere Zeile.
            .Where(entry => entry.DurationMinutes > 0)
            .Select(ToWorklogEntry)
            .OrderBy(entry => entry.Started)
            .ThenBy(entry => entry.ItemKey, StringComparer.Ordinal)
            .ToList();

        return new WorklogFetchResult(entries, Array.Empty<string>(), Array.Empty<string>());
    }

    private WorklogEntry ToWorklogEntry(AsanaTimeEntry entry) => new(
        WorklogSource.Asana,
        entry.AttributableTo?.Name is { Length: > 0 } project ? project : FallbackKey,
        TitleFor(entry),
        LocalMidnight(entry.EnteredOn),
        TimeSpan.FromMinutes(entry.DurationMinutes),
        Comment: string.Empty,
        UrlFor(entry),
        HasStartTime: false);

    /// <summary>
    /// Der Task-Name, wenn der Listenabruf ihn mitliefert; sonst die Kategorien, sonst der
    /// Projektname. Siehe Abschnitt 3.3 der Spezifikation: Ob der Task überhaupt kommt, hängt an
    /// Asanas Antwortschema und ist deshalb hier als „vielleicht" behandelt.
    /// </summary>
    private static string TitleFor(AsanaTimeEntry entry)
    {
        if (entry.Task?.Name is { Length: > 0 } taskName)
        {
            return taskName;
        }

        var categories = entry.Categories
            .Select(category => category.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        if (categories.Count > 0)
        {
            return string.Join(", ", categories);
        }

        return entry.AttributableTo?.Name ?? FallbackKey;
    }

    private string UrlFor(AsanaTimeEntry entry)
    {
        if (entry.Task?.PermalinkUrl is { Length: > 0 } permalink)
        {
            return permalink;
        }

        // Ohne Task bleibt das Projekt als Ziel; ohne Projekt der Workspace. Besser als ein toter
        // Verweis, und der Nutzer landet zumindest in der richtigen Umgebung.
        var gid = entry.AttributableTo?.Gid is { Length: > 0 } projectGid ? projectGid : _workspaceGid;
        return $"https://app.asana.com/0/{gid}";
    }

    private DateTimeOffset LocalMidnight(DateOnly date)
    {
        var localClock = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(localClock, _zone.GetUtcOffset(localClock));
    }
}
