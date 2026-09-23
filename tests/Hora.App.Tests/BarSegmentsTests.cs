using Hora.App.ViewModels;

namespace Hora.App.Tests;

/// <summary>
/// Die drei Abschnitte eines Tagesbalkens: grün bis zum eigenen Tagessoll, orange von dort bis zur
/// Zehnstundengrenze des Arbeitszeitgesetzes, rot darüber. Die Anteile beziehen sich auf die volle
/// Balkenbreite; Bezugswert ist der größere Wert aus Soll und erfasster Zeit — bis zum Soll misst
/// der Balken also wie bisher am Soll, darüber am Tag selbst.
/// </summary>
public class BarSegmentsTests
{
    private static readonly TimeSpan Target = TimeSpan.FromHours(8);
    private static readonly TimeSpan Limit = TimeSpan.FromHours(10);

    [Fact]
    public void Below_the_target_the_bar_stays_green_and_measures_against_the_target()
    {
        var segments = BarSegments.For(TimeSpan.FromHours(4), Target, Limit);

        Assert.Equal(0.5, segments.TargetShare, 6);
        Assert.Equal(0.0, segments.OvertimeShare, 6);
        Assert.Equal(0.0, segments.OverLimitShare, 6);
    }

    [Fact]
    public void Exactly_the_target_fills_the_whole_bar_green()
    {
        var segments = BarSegments.For(Target, Target, Limit);

        Assert.Equal(1.0, segments.TargetShare, 6);
        Assert.Equal(0.0, segments.OvertimeShare, 6);
        Assert.Equal(0.0, segments.OverLimitShare, 6);
    }

    [Fact]
    public void Beyond_the_target_the_extra_time_turns_orange()
    {
        var segments = BarSegments.For(TimeSpan.FromHours(9), Target, Limit);

        Assert.Equal(8.0 / 9.0, segments.TargetShare, 6);
        Assert.Equal(1.0 / 9.0, segments.OvertimeShare, 6);
        Assert.Equal(0.0, segments.OverLimitShare, 6);
    }

    [Fact]
    public void Exactly_ten_hours_is_still_orange()
    {
        var segments = BarSegments.For(TimeSpan.FromHours(10), Target, Limit);

        Assert.Equal(0.8, segments.TargetShare, 6);
        Assert.Equal(0.2, segments.OvertimeShare, 6);
        Assert.Equal(0.0, segments.OverLimitShare, 6);
    }

    [Fact]
    public void Beyond_ten_hours_only_the_hours_past_the_limit_turn_red()
    {
        // Elf Stunden bei acht Stunden Soll: acht grün, zwei orange, eine rot.
        var segments = BarSegments.For(TimeSpan.FromHours(11), Target, Limit);

        Assert.Equal(8.0 / 11.0, segments.TargetShare, 6);
        Assert.Equal(2.0 / 11.0, segments.OvertimeShare, 6);
        Assert.Equal(1.0 / 11.0, segments.OverLimitShare, 6);
    }

    [Fact]
    public void Without_a_target_every_booked_hour_counts_as_extra_time()
    {
        // Samstag oder abgewählter Wochentag: Soll null, also ist alles Mehrzeit.
        var segments = BarSegments.For(TimeSpan.FromHours(4), TimeSpan.Zero, Limit);

        Assert.Equal(0.0, segments.TargetShare, 6);
        Assert.Equal(1.0, segments.OvertimeShare, 6);
        Assert.Equal(0.0, segments.OverLimitShare, 6);
    }

    [Fact]
    public void A_target_beyond_the_limit_turns_red_at_ten_hours_all_the_same()
    {
        // Wer zwölf Stunden Soll einträgt, reißt die gesetzliche Grenze trotzdem ab der zehnten
        // Stunde: Rot gewinnt, Orange entfällt.
        var segments = BarSegments.For(TimeSpan.FromHours(11), TimeSpan.FromHours(12), Limit);

        Assert.Equal(10.0 / 12.0, segments.TargetShare, 6);
        Assert.Equal(0.0, segments.OvertimeShare, 6);
        Assert.Equal(1.0 / 12.0, segments.OverLimitShare, 6);
    }

    [Fact]
    public void Nothing_logged_leaves_the_bar_empty()
    {
        var segments = BarSegments.For(TimeSpan.Zero, Target, Limit);

        Assert.Equal(0.0, segments.TargetShare, 6);
        Assert.Equal(0.0, segments.OvertimeShare, 6);
        Assert.Equal(0.0, segments.OverLimitShare, 6);
    }

    [Fact]
    public void Neither_target_nor_time_divides_by_zero()
    {
        var segments = BarSegments.For(TimeSpan.Zero, TimeSpan.Zero, Limit);

        Assert.Equal(0.0, segments.TargetShare, 6);
        Assert.Equal(0.0, segments.OvertimeShare, 6);
        Assert.Equal(0.0, segments.OverLimitShare, 6);
    }

    [Fact]
    public void Without_a_limit_nothing_turns_red()
    {
        // Wochen- und Monatssumme kennen keine Zehnstundengrenze: Die gilt je Tag.
        var segments = BarSegments.For(TimeSpan.FromHours(50), TimeSpan.FromHours(40), limit: null);

        Assert.Equal(40.0 / 50.0, segments.TargetShare, 6);
        Assert.Equal(10.0 / 50.0, segments.OvertimeShare, 6);
        Assert.Equal(0.0, segments.OverLimitShare, 6);
    }

    [Fact]
    public void The_three_shares_never_add_up_to_more_than_the_full_bar()
    {
        TimeSpan[] hours = { TimeSpan.Zero, TimeSpan.FromHours(3), TimeSpan.FromHours(8), TimeSpan.FromHours(9.5), TimeSpan.FromHours(16) };

        Assert.All(hours, logged =>
        {
            var segments = BarSegments.For(logged, Target, Limit);
            var sum = segments.TargetShare + segments.OvertimeShare + segments.OverLimitShare;

            Assert.InRange(sum, 0.0, 1.0);
        });
    }
}
