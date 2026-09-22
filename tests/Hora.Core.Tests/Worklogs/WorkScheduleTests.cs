using Hora.Core.Worklogs;

namespace Hora.Core.Tests.Worklogs;

public class WorkScheduleTests
{
    [Fact]
    public void Default_is_eight_hours_monday_to_friday()
    {
        var schedule = WorkSchedule.Default();

        Assert.Equal(TimeSpan.FromHours(8), schedule.HoursFor(DayOfWeek.Monday));
        Assert.Equal(TimeSpan.FromHours(8), schedule.HoursFor(DayOfWeek.Friday));
        Assert.Equal(TimeSpan.Zero, schedule.HoursFor(DayOfWeek.Saturday));
        Assert.Equal(TimeSpan.Zero, schedule.HoursFor(DayOfWeek.Sunday));
    }

    [Fact]
    public void HoursFor_reads_the_matching_weekday()
    {
        var schedule = new WorkSchedule { Monday = 6, Tuesday = 0, Wednesday = 4.5 };

        Assert.Equal(TimeSpan.FromHours(6), schedule.HoursFor(DayOfWeek.Monday));
        Assert.Equal(TimeSpan.Zero, schedule.HoursFor(DayOfWeek.Tuesday));
        Assert.Equal(TimeSpan.FromHours(4.5), schedule.HoursFor(DayOfWeek.Wednesday));
    }

    [Fact]
    public void Negative_values_count_as_zero()
    {
        var schedule = new WorkSchedule { Monday = -3 };

        Assert.Equal(TimeSpan.Zero, schedule.HoursFor(DayOfWeek.Monday));
    }
}
