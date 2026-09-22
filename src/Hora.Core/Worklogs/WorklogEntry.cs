namespace Hora.Core.Worklogs;

/// <summary>Eine einzelne Zeitbuchung aus Jira, bereits auf den eigenen Nutzer gefiltert.</summary>
public sealed record WorklogEntry(
    string IssueKey,
    string IssueSummary,
    DateTimeOffset Started,
    TimeSpan Duration,
    string Comment)
{
    /// <summary>Kalendertag des Starts in der angegebenen Zeitzone.</summary>
    public DateOnly LocalDate(TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(Started, zone).DateTime);
}
