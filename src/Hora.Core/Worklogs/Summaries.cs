namespace Hora.Core.Worklogs;

/// <summary>Alle Buchungen eines Kalendertags, nach Startzeit sortiert.</summary>
public sealed record DaySummary(DateOnly Date, IReadOnlyList<WorklogEntry> Entries, TimeSpan Total);

/// <summary>Sieben Tagessummen von Montag bis Sonntag.</summary>
public sealed record WeekSummary(DateOnly WeekStart, IReadOnlyList<DaySummary> Days, TimeSpan Total);

/// <summary>Zwischensumme pro Ticket (Jira) oder pro Projekt (Asana).</summary>
public sealed record IssueTotal(
    WorklogSource Source,
    string ItemKey,
    string ItemSummary,
    TimeSpan Total,
    int EntryCount,
    string ItemUrl);

/// <summary>Ergebnis eines Abrufs: Einträge plus Tickets, deren Worklogs nicht geladen werden konnten.</summary>
public sealed record WorklogFetchResult(IReadOnlyList<WorklogEntry> Entries, IReadOnlyList<string> FailedIssueKeys);
