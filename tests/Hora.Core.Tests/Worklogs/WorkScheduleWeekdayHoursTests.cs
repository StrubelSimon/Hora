using Hora.Core.Worklogs;

namespace Hora.Core.Tests.Worklogs;

/// <summary>
/// Der Schrittschalter „Sollstunden pro Tag“ der Profilseite kennt nur einen Wert, das Modell hält
/// weiterhin sieben. Diese beiden Methoden bilden die Brücke: Lesen zeigt den ersten Werktag, der
/// nicht null ist; Schreiben setzt Montag bis Freitag gleichmäßig und lässt das Wochenende null.
/// </summary>
public class WorkScheduleWeekdayHoursTests
{
    [Fact]
    public void GetWeekdayHours_returns_the_uniform_value_of_the_default_schedule()
    {
        Assert.Equal(8, WorkSchedule.Default().GetWeekdayHours());
    }

    [Fact]
    public void GetWeekdayHours_returns_the_first_weekday_that_is_not_zero()
    {
        // Eine von Hand eingetragene Viertagewoche ohne Montag: Dienstag ist der erste Werktag > 0.
        var schedule = new WorkSchedule { Monday = 0, Tuesday = 6, Wednesday = 7, Thursday = 8, Friday = 0 };

        Assert.Equal(6, schedule.GetWeekdayHours());
    }

    [Fact]
    public void GetWeekdayHours_returns_zero_when_no_weekday_is_set()
    {
        var schedule = new WorkSchedule { Saturday = 5, Sunday = 5 };

        Assert.Equal(0, schedule.GetWeekdayHours());
    }

    [Fact]
    public void GetWeekdayHours_ignores_negative_values_from_a_hand_edited_file()
    {
        var schedule = new WorkSchedule { Monday = -3, Tuesday = 4 };

        Assert.Equal(4, schedule.GetWeekdayHours());
    }

    [Fact]
    public void SetWeekdayHours_sets_monday_to_friday_and_leaves_the_weekend_at_zero()
    {
        var schedule = new WorkSchedule { Monday = 1, Tuesday = 2, Wednesday = 3, Thursday = 4, Friday = 5, Saturday = 6, Sunday = 7 };

        schedule.SetWeekdayHours(7.5);

        Assert.Equal(7.5, schedule.Monday);
        Assert.Equal(7.5, schedule.Tuesday);
        Assert.Equal(7.5, schedule.Wednesday);
        Assert.Equal(7.5, schedule.Thursday);
        Assert.Equal(7.5, schedule.Friday);
        Assert.Equal(0, schedule.Saturday);
        Assert.Equal(0, schedule.Sunday);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(24, 24)]
    [InlineData(30, 24)]
    public void SetWeekdayHours_clamps_to_the_range_from_zero_to_twentyfour(double given, double expected)
    {
        var schedule = WorkSchedule.Default();

        schedule.SetWeekdayHours(given);

        Assert.Equal(expected, schedule.Monday);
    }

    [Fact]
    public void Unequal_weekdays_survive_as_long_as_SetWeekdayHours_is_not_called()
    {
        // Wer in settings.json ungleiche Tage einträgt, soll sie behalten. Das reine Lesen des
        // Anzeigewerts darf das Modell nicht verändern.
        var schedule = new WorkSchedule { Monday = 4, Tuesday = 8, Wednesday = 8, Thursday = 8, Friday = 6 };

        _ = schedule.GetWeekdayHours();

        Assert.Equal(4, schedule.Monday);
        Assert.Equal(8, schedule.Tuesday);
        Assert.Equal(6, schedule.Friday);
    }
}
