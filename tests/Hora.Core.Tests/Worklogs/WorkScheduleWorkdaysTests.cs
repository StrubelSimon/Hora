using Hora.Core.Worklogs;

namespace Hora.Core.Tests.Worklogs;

/// <summary>
/// Frei wählbare Arbeitstage: Welche Tage zur Woche gehören, steht nicht mehr fest. Als Arbeitstag
/// gilt jeder Tag mit einem Wert größer null; der Schrittschalter der Profilseite gilt für genau
/// diese Tage.
/// </summary>
public class WorkScheduleWorkdaysTests
{
    [Fact]
    public void The_week_runs_from_monday_to_sunday()
    {
        // Die Reihenfolge der sieben Umschalter auf der Profilseite hängt daran.
        Assert.Equal(
            new[]
            {
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday,
                DayOfWeek.Saturday,
                DayOfWeek.Sunday,
            },
            WorkSchedule.WeekOrder);
    }

    [Fact]
    public void The_default_week_has_five_workdays()
    {
        var schedule = WorkSchedule.Default();

        Assert.Equal(5, schedule.WorkdayCount);
        Assert.True(schedule.IsWorkday(DayOfWeek.Monday));
        Assert.True(schedule.IsWorkday(DayOfWeek.Friday));
        Assert.False(schedule.IsWorkday(DayOfWeek.Saturday));
        Assert.False(schedule.IsWorkday(DayOfWeek.Sunday));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-3, false)]
    [InlineData(0.5, true)]
    [InlineData(8, true)]
    public void A_day_above_zero_counts_as_a_workday(double hours, bool expected)
    {
        var schedule = new WorkSchedule();

        schedule.SetDayHours(DayOfWeek.Wednesday, hours);

        Assert.Equal(expected, schedule.IsWorkday(DayOfWeek.Wednesday));
    }

    [Fact]
    public void SetDayHours_changes_only_the_named_day()
    {
        var schedule = WorkSchedule.Default();

        schedule.SetDayHours(DayOfWeek.Saturday, 4);

        Assert.Equal(4, schedule.GetDayHours(DayOfWeek.Saturday));
        Assert.Equal(8, schedule.GetDayHours(DayOfWeek.Monday));
        Assert.Equal(0, schedule.GetDayHours(DayOfWeek.Sunday));
        Assert.Equal(6, schedule.WorkdayCount);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(7.5, 7.5)]
    [InlineData(24, 24)]
    [InlineData(30, 24)]
    public void SetDayHours_clamps_to_the_range_from_zero_to_twentyfour(double given, double expected)
    {
        var schedule = new WorkSchedule();

        schedule.SetDayHours(DayOfWeek.Monday, given);

        Assert.Equal(expected, schedule.GetDayHours(DayOfWeek.Monday));
    }

    [Fact]
    public void Deselecting_a_day_sets_it_to_zero_and_lowers_the_weekly_target()
    {
        var schedule = WorkSchedule.Default();

        schedule.SetDayHours(DayOfWeek.Friday, 0);

        Assert.False(schedule.IsWorkday(DayOfWeek.Friday));
        Assert.Equal(4, schedule.WorkdayCount);
        Assert.Equal(
            TimeSpan.FromHours(32),
            TargetCalculator.ForRange(schedule, new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20)));
    }

    [Fact]
    public void GetWorkdayHours_returns_the_first_workday_that_is_not_zero()
    {
        var schedule = new WorkSchedule { Monday = 0, Tuesday = 6, Wednesday = 7 };

        Assert.Equal(6, schedule.GetWorkdayHours());
    }

    [Fact]
    public void GetWorkdayHours_also_sees_a_week_that_consists_of_weekend_days_only()
    {
        // Genau das kann seit den frei wählbaren Arbeitstagen entstehen; GetWeekdayHours sieht
        // ausschließlich Montag bis Freitag und lieferte hier null.
        var schedule = new WorkSchedule { Saturday = 5, Sunday = 3 };

        Assert.Equal(5, schedule.GetWorkdayHours());
        Assert.Equal(2, schedule.WorkdayCount);
    }

    [Fact]
    public void GetWorkdayHours_returns_zero_when_no_day_is_set()
    {
        Assert.Equal(0, new WorkSchedule().GetWorkdayHours());
    }

    [Fact]
    public void A_week_without_any_workday_has_a_weekly_target_of_zero()
    {
        var schedule = new WorkSchedule();

        var target = TargetCalculator.ForRange(schedule, new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20));

        Assert.Equal(0, schedule.WorkdayCount);
        Assert.Equal(TimeSpan.Zero, target);
    }

    [Fact]
    public void Unequal_values_from_the_file_survive_a_pure_read()
    {
        var schedule = new WorkSchedule { Monday = 0, Tuesday = 6, Wednesday = 7, Thursday = 8, Friday = 4, Saturday = 2 };

        _ = schedule.GetWorkdayHours();
        _ = schedule.WorkdayCount;
        _ = schedule.IsWorkday(DayOfWeek.Monday);

        Assert.Equal(0, schedule.Monday);
        Assert.Equal(6, schedule.Tuesday);
        Assert.Equal(7, schedule.Wednesday);
        Assert.Equal(8, schedule.Thursday);
        Assert.Equal(4, schedule.Friday);
        Assert.Equal(2, schedule.Saturday);
    }

    [Fact]
    public void GetDayHours_shows_a_hand_edited_negative_value_unchanged()
    {
        // Gelesen wird der Rohwert; erst HoursFor macht daraus null für die Rechnung.
        var schedule = new WorkSchedule { Monday = -3 };

        Assert.Equal(-3, schedule.GetDayHours(DayOfWeek.Monday));
        Assert.Equal(TimeSpan.Zero, schedule.HoursFor(DayOfWeek.Monday));
    }
}
