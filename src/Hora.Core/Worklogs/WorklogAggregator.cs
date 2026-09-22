namespace Hora.Core.Worklogs;

/// <summary>Reine Funktionen zum Summieren und Gruppieren von Buchungen. Kein I/O.</summary>
public static class WorklogAggregator
{
    public static DaySummary BuildDaySummary(IEnumerable<WorklogEntry> entries, DateOnly date, TimeZoneInfo zone)
    {
        var dayEntries = entries
            .Where(entry => entry.LocalDate(zone) == date)
            .OrderBy(entry => entry.Started)
            .ToList();

        return new DaySummary(date, dayEntries, Sum(dayEntries.Select(entry => entry.Duration)));
    }

    public static WeekSummary BuildWeekSummary(IEnumerable<WorklogEntry> entries, DateOnly anyDayInWeek, TimeZoneInfo zone)
    {
        var weekStart = WeekCalendar.StartOfWeek(anyDayInWeek);
        var materialized = entries as IReadOnlyCollection<WorklogEntry> ?? entries.ToList();

        var days = Enumerable.Range(0, 7)
            .Select(offset => BuildDaySummary(materialized, weekStart.AddDays(offset), zone))
            .ToList();

        return new WeekSummary(weekStart, days, Sum(days.Select(day => day.Total)));
    }

    public static IReadOnlyList<IssueTotal> BuildIssueTotals(IEnumerable<WorklogEntry> entries)
    {
        // Gruppiert wird nach Quelle UND Schlüssel: Ein Jira-Ticket und ein Asana-Projekt können
        // denselben Text tragen, ihre Zeiten gehören aber nicht in dieselbe Zeile.
        return entries
            .GroupBy(entry => (entry.Source, entry.ItemKey))
            .Select(group => new IssueTotal(
                group.Key.Source,
                group.Key.ItemKey,
                group.First().ItemSummary,
                Sum(group.Select(entry => entry.Duration)),
                group.Count(),
                group.First().ItemUrl))
            .OrderByDescending(total => total.Total)
            .ThenBy(total => total.ItemKey, StringComparer.Ordinal)
            .ThenBy(total => total.Source)
            .ToList();
    }

    private static TimeSpan Sum(IEnumerable<TimeSpan> durations) =>
        durations.Aggregate(TimeSpan.Zero, (accumulated, duration) => accumulated + duration);
}
