using Hora.Core.Jira;

namespace Hora.Core.Tests.Jira;

public class JiraDateParserTests
{
    [Theory]
    [InlineData("2021-01-17T12:34:00.000+0000", 0)]
    [InlineData("2026-09-16T09:15:00.000+0200", 120)]
    [InlineData("2026-09-16T09:15:00.000-0500", -300)]
    [InlineData("2026-09-16T09:15:00.000+02:00", 120)]
    [InlineData("2026-09-16T09:15:00.000Z", 0)]
    public void Parse_reads_offset_with_or_without_colon(string value, int expectedOffsetMinutes)
    {
        var result = JiraDateParser.Parse(value);

        Assert.Equal(TimeSpan.FromMinutes(expectedOffsetMinutes), result.Offset);
    }

    [Fact]
    public void Parse_keeps_wall_clock_time_of_the_given_offset()
    {
        var result = JiraDateParser.Parse("2026-09-16T09:15:30.000+0200");

        Assert.Equal(new DateTimeOffset(2026, 9, 16, 9, 15, 30, TimeSpan.FromHours(2)), result);
    }

    [Fact]
    public void Parse_accepts_timestamps_without_milliseconds()
    {
        var result = JiraDateParser.Parse("2026-09-16T09:15:00+0200");

        Assert.Equal(new DateTimeOffset(2026, 9, 16, 9, 15, 0, TimeSpan.FromHours(2)), result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("quatsch")]
    [InlineData("2026-09-16")]
    public void Parse_rejects_invalid_input(string value)
    {
        Assert.Throws<FormatException>(() => JiraDateParser.Parse(value));
    }
}
