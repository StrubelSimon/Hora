namespace Hora.Core.Worklogs;

/// <summary>Alle Buchungen eines Kalendertags, nach Startzeit sortiert.</summary>
public sealed record DaySummary(DateOnly Date, IReadOnlyList<WorklogEntry> Entries, TimeSpan Total);

/// <summary>Sieben Tagessummen von Montag bis Sonntag.</summary>
public sealed record WeekSummary(DateOnly WeekStart, IReadOnlyList<DaySummary> Days, TimeSpan Total);

/// <summary>Zwischensumme pro Ticket.</summary>
public sealed record IssueTotal(string IssueKey, string IssueSummary, TimeSpan Total, int EntryCount);

/// <summary>Ergebnis eines Abrufs: Einträge plus Tickets, deren Worklogs nicht geladen werden konnten.</summary>
public sealed record WorklogFetchResult(IReadOnlyList<WorklogEntry> Entries, IReadOnlyList<string> FailedIssueKeys);
