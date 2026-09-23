using Hora.App.ViewModels;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

/// <summary>
/// Der Balken einer Tageszeile der Wochenansicht (Abschnitt 3.5 der Gestaltungsvorgaben): grün bis
/// zum Tagessoll, orange für die Mehrzeit, rot ab der zehnten Stunde.
/// </summary>
public class WeekDayRowTests
{
    private static readonly DateOnly Wednesday = new(2026, 9, 16);
    private static readonly DateOnly Saturday = new(2026, 9, 19);
    private static readonly TimeSpan Target = TimeSpan.FromHours(8);

    private static DaySummary Day(DateOnly date, double hours)
    {
        var duration = TimeSpan.FromHours(hours);
        var entry = WorklogEntry.Jira("SYN-1", "Titel", new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), duration, string.Empty, "https://example.atlassian.net");

        return new DaySummary(date, new[] { entry }, duration);
    }

    [Fact]
    public void A_day_below_its_target_stays_green_and_measures_against_the_target()
    {
        var row = WeekDayRow.From(Day(Wednesday, 4), Wednesday, Target);

        Assert.Equal(0.5, row.Segments.TargetShare, 6);
        Assert.Equal(0.0, row.Segments.OvertimeShare, 6);
        Assert.Equal(0.0, row.Segments.OverLimitShare, 6);
    }

    [Fact]
    public void A_day_beyond_its_target_shows_the_extra_time_in_orange()
    {
        var row = WeekDayRow.From(Day(Wednesday, 10), Wednesday, Target);

        Assert.Equal(0.8, row.Segments.TargetShare, 6);
        Assert.Equal(0.2, row.Segments.OvertimeShare, 6);
        Assert.Equal(0.0, row.Segments.OverLimitShare, 6);
    }

    [Fact]
    public void A_day_beyond_ten_hours_shows_the_hours_past_the_limit_in_red()
    {
        var row = WeekDayRow.From(Day(Wednesday, 11), Wednesday, Target);

        Assert.Equal(8.0 / 11.0, row.Segments.TargetShare, 6);
        Assert.Equal(2.0 / 11.0, row.Segments.OvertimeShare, 6);
        Assert.Equal(1.0 / 11.0, row.Segments.OverLimitShare, 6);
    }

    [Fact]
    public void A_day_without_a_target_counts_every_booked_hour_as_extra_time()
    {
        // Samstag: kein Soll, also ist die gesamte Buchung Mehrzeit.
        var row = WeekDayRow.From(Day(Saturday, 3), Wednesday, TimeSpan.Zero);

        Assert.Equal(0.0, row.Segments.TargetShare, 6);
        Assert.Equal(1.0, row.Segments.OvertimeShare, 6);
        Assert.Equal(0.0, row.Segments.OverLimitShare, 6);
    }
}
