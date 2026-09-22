namespace Hora.Core.Worklogs;

/// <summary>Eine einzelne Zeitbuchung, bereits auf den eigenen Nutzer gefiltert.</summary>
public sealed record WorklogEntry(
    WorklogSource Source,
    string ItemKey,
    string ItemSummary,
    DateTimeOffset Started,
    TimeSpan Duration,
    string Comment,
    string ItemUrl,
    bool HasStartTime)
{
    /// <summary>Kalendertag des Starts in der angegebenen Zeitzone.</summary>
    public DateOnly LocalDate(TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(Started, zone).DateTime);

    /// <summary>
    /// Eine Jira-Buchung. Die Fabrik baut die Ticketadresse an einer einzigen Stelle und setzt
    /// <see cref="HasStartTime"/> auf wahr — Jira-Worklogs tragen immer eine Uhrzeit.
    /// </summary>
    public static WorklogEntry Jira(
        string itemKey,
        string itemSummary,
        DateTimeOffset started,
        TimeSpan duration,
        string comment,
        string baseUrl) => new(
        WorklogSource.Jira,
        itemKey,
        itemSummary,
        started,
        duration,
        comment,
        $"{baseUrl.TrimEnd('/')}/browse/{itemKey}",
        HasStartTime: true);
}
