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
        return entries
            .GroupBy(entry => entry.IssueKey, StringComparer.Ordinal)
            .Select(group => new IssueTotal(
                group.Key,
                group.First().IssueSummary,
                Sum(group.Select(entry => entry.Duration)),
                group.Count()))
            .OrderByDescending(total => total.Total)
            .ThenBy(total => total.IssueKey, StringComparer.Ordinal)
            .ToList();
    }

    private static TimeSpan Sum(IEnumerable<TimeSpan> durations) =>
        durations.Aggregate(TimeSpan.Zero, (accumulated, duration) => accumulated + duration);
}
