using Hora.Core.Worklogs;

namespace Hora.Core.Tests.Worklogs;

public class WorklogAggregatorTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    private static readonly TimeSpan Cest = TimeSpan.FromHours(2);

    private const string BaseUrl = "https://example.atlassian.net";

    private static WorklogEntry Entry(string itemKey, int day, int hour, int minute, int durationMinutes, string summary = "Titel") =>
        WorklogEntry.Jira(itemKey, summary, new DateTimeOffset(2026, 9, day, hour, minute, 0, Cest), TimeSpan.FromMinutes(durationMinutes), string.Empty, BaseUrl);

    [Fact]
    public void BuildDaySummary_keeps_only_entries_of_that_day_sorted_by_start()
    {
        var entries = new[]
        {
            Entry("ABC-1", 16, 14, 0, 60),
            Entry("ABC-2", 16, 9, 0, 30),
            Entry("ABC-3", 17, 10, 0, 45),
        };

        var summary = WorklogAggregator.BuildDaySummary(entries, new DateOnly(2026, 9, 16), Berlin);

        Assert.Equal(new DateOnly(2026, 9, 16), summary.Date);
        Assert.Equal(new[] { "ABC-2", "ABC-1" }, summary.Entries.Select(e => e.ItemKey));
        Assert.Equal(TimeSpan.FromMinutes(90), summary.Total);
    }

    [Fact]
    public void BuildDaySummary_uses_local_midnight_as_boundary()
    {
        var entries = new[]
        {
            Entry("ABC-1", 16, 23, 59, 10),
            Entry("ABC-2", 17, 0, 0, 10),
        };

        var summary = WorklogAggregator.BuildDaySummary(entries, new DateOnly(2026, 9, 16), Berlin);

        Assert.Equal(new[] { "ABC-1" }, summary.Entries.Select(e => e.ItemKey));
    }

    [Fact]
    public void BuildDaySummary_converts_foreign_offsets_into_the_given_zone()
    {
        // 22:30 UTC am 16.9. ist 00:30 am 17.9. in Berlin (Sommerzeit).
        var lateUtc = WorklogEntry.Jira("ABC-1", "Titel",
            new DateTimeOffset(2026, 9, 16, 22, 30, 0, TimeSpan.Zero), TimeSpan.FromMinutes(15), string.Empty, BaseUrl);

        var day16 = WorklogAggregator.BuildDaySummary(new[] { lateUtc }, new DateOnly(2026, 9, 16), Berlin);
        var day17 = WorklogAggregator.BuildDaySummary(new[] { lateUtc }, new DateOnly(2026, 9, 17), Berlin);

        Assert.Empty(day16.Entries);
        Assert.Single(day17.Entries);
    }

    [Fact]
    public void BuildWeekSummary_always_contains_seven_days_from_monday()
    {
        var summary = WorklogAggregator.BuildWeekSummary(Array.Empty<WorklogEntry>(), new DateOnly(2026, 9, 16), Berlin);

        Assert.Equal(new DateOnly(2026, 9, 14), summary.WeekStart);
        Assert.Equal(7, summary.Days.Count);
        Assert.Equal(Enumerable.Range(14, 7).Select(d => new DateOnly(2026, 9, d)), summary.Days.Select(d => d.Date));
        Assert.All(summary.Days, d => Assert.Equal(TimeSpan.Zero, d.Total));
        Assert.Equal(TimeSpan.Zero, summary.Total);
    }

    [Fact]
    public void BuildWeekSummary_sums_only_entries_inside_the_week()
    {
        var entries = new[]
        {
            Entry("ABC-1", 14, 9, 0, 60),  // Montag
            Entry("ABC-2", 18, 9, 0, 30),  // Freitag
            Entry("ABC-3", 21, 9, 0, 999), // Montag der Folgewoche
        };

        var summary = WorklogAggregator.BuildWeekSummary(entries, new DateOnly(2026, 9, 16), Berlin);

        Assert.Equal(TimeSpan.FromMinutes(90), summary.Total);
        Assert.Equal(TimeSpan.FromMinutes(60), summary.Days[0].Total);
        Assert.Equal(TimeSpan.FromMinutes(30), summary.Days[4].Total);
    }

    [Fact]
    public void BuildIssueTotals_groups_by_issue_and_sorts_by_total_descending()
    {
        var entries = new[]
        {
            Entry("ABC-1", 16, 9, 0, 30, "Erstes"),
            Entry("ABC-2", 16, 10, 0, 120, "Zweites"),
            Entry("ABC-1", 16, 11, 0, 60, "Erstes"),
        };

        var totals = WorklogAggregator.BuildIssueTotals(entries);

        Assert.Equal(2, totals.Count);
        Assert.Equal("ABC-2", totals[0].ItemKey);
        Assert.Equal(TimeSpan.FromMinutes(120), totals[0].Total);
        Assert.Equal(1, totals[0].EntryCount);
        Assert.Equal("ABC-1", totals[1].ItemKey);
        Assert.Equal("Erstes", totals[1].ItemSummary);
        Assert.Equal(TimeSpan.FromMinutes(90), totals[1].Total);
        Assert.Equal(2, totals[1].EntryCount);
    }

    [Fact]
    public void BuildIssueTotals_breaks_ties_by_issue_key()
    {
        var entries = new[] { Entry("ZZZ-1", 16, 9, 0, 60), Entry("ABC-1", 16, 10, 0, 60) };

        var totals = WorklogAggregator.BuildIssueTotals(entries);

        Assert.Equal(new[] { "ABC-1", "ZZZ-1" }, totals.Select(t => t.ItemKey));
    }
}
