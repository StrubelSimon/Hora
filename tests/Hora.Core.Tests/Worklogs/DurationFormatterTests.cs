using Hora.Core.Worklogs;

namespace Hora.Core.Tests.Worklogs;

public class DurationFormatterTests
{
    [Theory]
    [InlineData(0, "0m")]
    [InlineData(45, "45m")]
    [InlineData(60, "1h")]
    [InlineData(450, "7h 30m")]
    [InlineData(1500, "25h")]
    public void Format_renders_hours_and_minutes(int minutes, string expected)
    {
        var result = DurationFormatter.Format(TimeSpan.FromMinutes(minutes));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_truncates_seconds_to_whole_minutes()
    {
        Assert.Equal("1m", DurationFormatter.Format(TimeSpan.FromSeconds(90)));
    }

    [Fact]
    public void Format_treats_negative_durations_as_zero()
    {
        Assert.Equal("0m", DurationFormatter.Format(TimeSpan.FromMinutes(-5)));
    }
}
