using Hora.Core.Worklogs;

namespace Hora.Core.Tests.Worklogs;

public class TargetCalculatorTests
{
    private static readonly WorkSchedule Standard = WorkSchedule.Default();

    [Fact]
    public void Single_weekday_is_eight_hours()
    {
        // 17.09.2026 ist ein Donnerstag.
        var date = new DateOnly(2026, 9, 17);

        Assert.Equal(TimeSpan.FromHours(8), TargetCalculator.ForRange(Standard, date, date));
    }

    [Fact]
    public void Single_weekend_day_is_zero()
    {
        var saturday = new DateOnly(2026, 9, 19);

        Assert.Equal(TimeSpan.Zero, TargetCalculator.ForRange(Standard, saturday, saturday));
    }

    [Fact]
    public void Full_week_matches_the_prototype_value_of_forty_hours()
    {
        var from = new DateOnly(2026, 9, 14);
        var to = new DateOnly(2026, 9, 20);

        Assert.Equal(TimeSpan.FromHours(40), TargetCalculator.ForRange(Standard, from, to));
    }

    [Fact]
    public void September_2026_matches_the_prototype_value_of_176_hours()
    {
        var from = new DateOnly(2026, 9, 1);
        var to = new DateOnly(2026, 9, 30);

        Assert.Equal(TimeSpan.FromHours(176), TargetCalculator.ForRange(Standard, from, to));
    }

    [Fact]
    public void Reversed_range_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            TargetCalculator.ForRange(Standard, new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 14)));
    }

    [Fact]
    public void Custom_schedule_is_honoured()
    {
        var parttime = new WorkSchedule { Monday = 8, Tuesday = 8, Wednesday = 4, Thursday = 0, Friday = 0 };

        var week = TargetCalculator.ForRange(parttime, new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20));

        Assert.Equal(TimeSpan.FromHours(20), week);
    }
}
