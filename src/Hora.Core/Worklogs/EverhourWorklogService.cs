using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Hora.Core.Everhour;
using Hora.Core.Everhour.Models;

namespace Hora.Core.Worklogs;

/// <summary>
/// Der Anbieter der Asana-Zeiten hinter <see cref="IWorklogService"/>. Die Zeiten kommen aus
/// Everhour, weil dort gebucht wird; Asanas eigene Zeiterfassung bleibt leer.
/// </summary>
/// <remarks>
/// Hier fallen die Everhour-Einträge weg, die ohne jeden Blick auf Jira als Dopplung erkennbar
/// sind (Regeln 1 und 2). Der Abgleich gegen die tatsächlich geladenen Jira-Buchungen folgt im
/// <see cref="CompositeWorklogService"/> über <see cref="JiraDuplicateFilter"/>.
/// </remarks>
public sealed class EverhourWorklogService : IWorklogService
{
    /// <summary>Ersatzkennung für einen Eintrag, dessen Projektname sich nicht ermitteln ließ.</summary>
    private const string FallbackKey = "Asana";

    private const string AsanaTaskPrefix = "as:";

    private const string FallbackUrl = "https://app.asana.com/";

    /// <summary>
    /// Kennzeichen einer Kopie eines Jira-Worklogs, etwa „[SYN-2204#wl-42234]“. Solche Einträge
    /// entstanden, als Jira-Buchungen in die Asana-Struktur übernommen wurden.
    /// </summary>
    private static readonly Regex JiraWorklogMirrorMarker = new(@"#wl-\d+", RegexOptions.CultureInvariant);

    private readonly IEverhourClient _client;
    private readonly long _userId;
    private readonly TimeZoneInfo _zone;

    // Projektnamen ändern sich kaum, und ohne Zwischenspeicher kostete jede Aktualisierung einen
    // Aufruf je Projekt. Nur Treffer werden gemerkt: Ein vorübergehender Fehler soll beim nächsten
    // Abruf erneut versucht werden.
    private readonly ConcurrentDictionary<string, string> _projectNames = new(StringComparer.Ordinal);

    public EverhourWorklogService(IEverhourClient client, long userId, TimeZoneInfo zone)
    {
        _client = client;
        _userId = userId;
        _zone = zone;
    }

    public async Task<WorklogFetchResult> GetEntriesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from)
        {
            throw new ArgumentException("Das Enddatum liegt vor dem Startdatum.", nameof(to));
        }

        var timeEntries = await _client.GetTimeEntriesAsync(_userId, from, to, cancellationToken);

        var entries = new List<WorklogEntry>();
        foreach (var entry in timeEntries.Where(IsOwnAsanaTime))
        {
            var projectName = await ProjectNameForAsync(entry.Task!, cancellationToken);
            entries.Add(ToWorklogEntry(entry, projectName));
        }

        return new WorklogFetchResult(
            entries
                .OrderBy(entry => entry.Started)
                .ThenBy(entry => entry.ItemSummary, StringComparer.Ordinal)
                .ThenBy(entry => entry.ItemUrl, StringComparer.Ordinal)
                .ToList(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    /// <summary>
    /// Regel 1: nur Asana-Aufgaben mit Dauer — Everhours eigene Jira-Aufgaben (<c>jr:</c>) sind
    /// Abbilder der Jira-Worklogs, und für die ist allein Jira die Quelle.
    /// Regel 2: keine Kopien von Jira-Worklogs.
    /// </summary>
    private static bool IsOwnAsanaTime(EverhourTimeEntry entry) =>
        entry.Time > 0
        && entry.Task is { } task
        && task.Id.StartsWith(AsanaTaskPrefix, StringComparison.Ordinal)
        && !(entry.Comment is { } comment && JiraWorklogMirrorMarker.IsMatch(comment));

    private async Task<string> ProjectNameForAsync(EverhourTask task, CancellationToken cancellationToken)
    {
        var projectId = task.Projects.FirstOrDefault();
        if (string.IsNullOrEmpty(projectId))
        {
            return FallbackKey;
        }

        if (_projectNames.TryGetValue(projectId, out var cached))
        {
            return cached;
        }

        try
        {
            var project = await _client.GetProjectAsync(projectId, cancellationToken);
            if (string.IsNullOrWhiteSpace(project.Name))
            {
                return FallbackKey;
            }

            _projectNames[projectId] = project.Name;
            return project.Name;
        }
        catch (EverhourAuthenticationException)
        {
            throw; // Ein abgelehnter Schlüssel betrifft den ganzen Abruf, nicht nur das Abzeichen.
        }
        catch (EverhourException)
        {
            // Ohne Projektname fehlt nur das Abzeichen; die Zeit selbst ist bekannt und zählt.
            return FallbackKey;
        }
    }

    private WorklogEntry ToWorklogEntry(EverhourTimeEntry entry, string projectName) => new(
        WorklogSource.Asana,
        projectName,
        entry.Task!.Name,
        LocalMidnight(entry.Date),
        TimeSpan.FromSeconds(entry.Time),
        entry.Comment ?? string.Empty,
        entry.Task.Url is { Length: > 0 } url ? url : FallbackUrl,
        HasStartTime: false);

    /// <summary>
    /// An Tagen, an denen die Sommerzeit um Mitternacht umschaltet, existiert 00:00 Uhr Ortszeit
    /// nicht oder doppelt. <see cref="TimeZoneInfo.GetUtcOffset(DateTime)"/> liefert auch dann
    /// einen gültigen Versatz; der Kalendertag bleibt der gebuchte.
    /// </summary>
    private DateTimeOffset LocalMidnight(DateOnly date)
    {
        var localClock = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(localClock, _zone.GetUtcOffset(localClock));
    }
}
