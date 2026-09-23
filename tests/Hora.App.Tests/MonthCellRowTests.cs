using Hora.App.ViewModels;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

/// <summary>
/// Der schmale Balken einer Tageszelle der Monatsansicht trägt dieselben drei Abschnitte wie die
/// Tageszeile der Wochenansicht.
/// </summary>
public class MonthCellRowTests
{
    private static readonly DateOnly Wednesday = new(2026, 9, 16);
    private static readonly DateOnly Saturday = new(2026, 9, 19);

    private static DaySummary Day(DateOnly date, double hours)
    {
        var duration = TimeSpan.FromHours(hours);
        var entry = WorklogEntry.Jira("SYN-1", "Titel", new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), duration, string.Empty, "https://example.atlassian.net");

        return new DaySummary(date, new[] { entry }, duration);
    }

    private static MonthCellRow Cell(DateOnly date, double hours) =>
        MonthCellRow.From(Day(date, hours), WorkSchedule.Default(), Wednesday, Wednesday);

    [Fact]
    public void A_day_below_its_target_stays_green()
    {
        var cell = Cell(Wednesday, 2);

        Assert.Equal(0.25, cell.Segments.TargetShare, 6);
        Assert.Equal(0.0, cell.Segments.OvertimeShare, 6);
        Assert.Equal(0.0, cell.Segments.OverLimitShare, 6);
    }

    [Fact]
    public void A_day_beyond_its_target_shows_the_extra_time_in_orange()
    {
        var cell = Cell(Wednesday, 9);

        Assert.Equal(8.0 / 9.0, cell.Segments.TargetShare, 6);
        Assert.Equal(1.0 / 9.0, cell.Segments.OvertimeShare, 6);
        Assert.Equal(0.0, cell.Segments.OverLimitShare, 6);
    }

    [Fact]
    public void A_day_beyond_ten_hours_shows_the_hours_past_the_limit_in_red()
    {
        var cell = Cell(Wednesday, 12);

        Assert.Equal(8.0 / 12.0, cell.Segments.TargetShare, 6);
        Assert.Equal(2.0 / 12.0, cell.Segments.OvertimeShare, 6);
        Assert.Equal(2.0 / 12.0, cell.Segments.OverLimitShare, 6);
    }

    [Fact]
    public void A_day_without_a_target_counts_every_booked_hour_as_extra_time()
    {
        var cell = Cell(Saturday, 4);

        Assert.Equal(0.0, cell.Segments.TargetShare, 6);
        Assert.Equal(1.0, cell.Segments.OvertimeShare, 6);
        Assert.Equal(0.0, cell.Segments.OverLimitShare, 6);
    }
}
