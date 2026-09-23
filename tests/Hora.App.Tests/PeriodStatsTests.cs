using Hora.App.ViewModels;

namespace Hora.App.Tests;

public class PeriodStatsTests
{
    [Fact]
    public void Exact_match_yields_hundred_percent_and_zero_difference_without_sign()
    {
        var stats = PeriodStats.For(TimeSpan.FromHours(8), TimeSpan.FromHours(8));

        Assert.Equal("100 %", stats.PercentText);
        Assert.Equal("0h", stats.DifferenceText);
        Assert.False(stats.IsNegative);
        Assert.Equal(1.0, stats.Segments.TargetShare, 3);
    }

    [Fact]
    public void Undershooting_the_target_yields_a_negative_difference_with_a_real_minus_sign()
    {
        var stats = PeriodStats.For(TimeSpan.FromHours(28) + TimeSpan.FromMinutes(30), TimeSpan.FromHours(40));

        Assert.Equal("71 %", stats.PercentText);
        Assert.Equal("−11h 30m", stats.DifferenceText);
        Assert.True(stats.IsNegative);
    }

    [Fact]
    public void Zero_target_yields_a_dash_for_percent_and_counts_everything_as_extra_time()
    {
        var stats = PeriodStats.For(TimeSpan.FromHours(2), TimeSpan.Zero);

        Assert.Equal("–", stats.PercentText);
        Assert.Equal(0.0, stats.Segments.TargetShare, 3);
        Assert.Equal(1.0, stats.Segments.OvertimeShare, 3);
    }

    [Fact]
    public void Overshooting_the_target_shows_the_real_percentage_and_the_extra_time_in_orange()
    {
        var stats = PeriodStats.For(TimeSpan.FromHours(12), TimeSpan.FromHours(8));

        Assert.Equal("150 %", stats.PercentText);
        Assert.Equal(8.0 / 12.0, stats.Segments.TargetShare, 3);
        Assert.Equal(4.0 / 12.0, stats.Segments.OvertimeShare, 3);
        Assert.Equal(0.0, stats.Segments.OverLimitShare, 3);
    }

    [Fact]
    public void A_week_knows_no_ten_hour_limit_because_that_limit_is_a_daily_one()
    {
        var stats = PeriodStats.For(TimeSpan.FromHours(50), TimeSpan.FromHours(40));

        Assert.Equal(0.8, stats.Segments.TargetShare, 3);
        Assert.Equal(0.2, stats.Segments.OvertimeShare, 3);
        Assert.Equal(0.0, stats.Segments.OverLimitShare, 3);
    }

    [Fact]
    public void A_single_day_turns_red_beyond_the_given_limit()
    {
        var stats = PeriodStats.For(
            TimeSpan.FromHours(11),
            TimeSpan.FromHours(8),
            limit: TimeSpan.FromHours(10));

        Assert.Equal(8.0 / 11.0, stats.Segments.TargetShare, 3);
        Assert.Equal(2.0 / 11.0, stats.Segments.OvertimeShare, 3);
        Assert.Equal(1.0 / 11.0, stats.Segments.OverLimitShare, 3);
    }

    [Fact]
    public void LoggedText_and_TargetText_use_the_shared_duration_format()
    {
        var stats = PeriodStats.For(TimeSpan.FromMinutes(90), TimeSpan.FromHours(8));

        Assert.Equal("1h 30m", stats.LoggedText);
        Assert.Equal("8h", stats.TargetText);
    }
}
