using Hora.Core.Worklogs;

namespace Hora.Core.Tests.Worklogs;

/// <summary>
/// Zweite Darstellung der Dauer: Dezimalstunden („7,75h“) neben der bisherigen Schreibweise
/// („7h 45m“). Das Format wird als Parameter hereingereicht; es gibt bewusst keinen statischen
/// veränderlichen Zustand, der die Tests voneinander abhängig machen würde.
/// </summary>
public class DurationFormatDecimalTests
{
    [Theory]
    [InlineData(0, "0m")]
    [InlineData(60, "1h")]
    [InlineData(465, "7h 45m")]
    [InlineData(1485, "24h 45m")]
    [InlineData(1500, "25h")]
    public void HoursMinutes_format_matches_the_previous_rendering(int minutes, string expected)
    {
        var result = DurationFormatter.Format(TimeSpan.FromMinutes(minutes), DurationFormat.HoursMinutes);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, "0h")]           // null
    [InlineData(60, "1h")]          // glatte Stunde: keine Nachkommastellen
    [InlineData(480, "8h")]         // glatte Stunden
    [InlineData(15, "0,25h")]       // Viertelstunde
    [InlineData(30, "0,50h")]       // halbe Stunde
    [InlineData(45, "0,75h")]       // Dreiviertelstunde
    [InlineData(465, "7,75h")]      // 7h 45m aus dem Entwurf
    [InlineData(450, "7,50h")]      // 7h 30m
    [InlineData(1485, "24,75h")]    // über 24 Stunden mit Rest
    [InlineData(1500, "25h")]       // über 24 Stunden, glatt
    public void DecimalHours_format_uses_two_decimals_with_a_comma(int minutes, string expected)
    {
        var result = DurationFormatter.Format(TimeSpan.FromMinutes(minutes), DurationFormat.DecimalHours);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void DecimalHours_truncates_seconds_to_whole_minutes()
    {
        // 7h 45m 59s bleibt 7,75h: Sekunden werden abgeschnitten wie in der bisherigen Darstellung.
        var duration = TimeSpan.FromMinutes(465) + TimeSpan.FromSeconds(59);

        Assert.Equal("7,75h", DurationFormatter.Format(duration, DurationFormat.DecimalHours));
    }

    [Fact]
    public void DecimalHours_treats_negative_durations_as_zero()
    {
        Assert.Equal("0h", DurationFormatter.Format(TimeSpan.FromMinutes(-5), DurationFormat.DecimalHours));
    }

    [Fact]
    public void Default_parameter_keeps_the_previous_rendering()
    {
        // Die vorhandenen Aufrufstellen und Tests rufen Format(TimeSpan) ohne Format auf.
        Assert.Equal("7h 45m", DurationFormatter.Format(TimeSpan.FromMinutes(465)));
    }

    [Theory]
    [InlineData("HoursMinutes", DurationFormat.HoursMinutes)]
    [InlineData("DecimalHours", DurationFormat.DecimalHours)]
    [InlineData("decimalhours", DurationFormat.DecimalHours)]
    [InlineData("", DurationFormat.HoursMinutes)]
    [InlineData(null, DurationFormat.HoursMinutes)]
    [InlineData("Unsinn", DurationFormat.HoursMinutes)]
    public void Parse_falls_back_to_the_previous_rendering(string? stored, DurationFormat expected)
    {
        Assert.Equal(expected, DurationFormatter.Parse(stored));
    }

    [Fact]
    public void Parse_round_trips_the_stored_name()
    {
        Assert.Equal(DurationFormat.DecimalHours, DurationFormatter.Parse(DurationFormat.DecimalHours.ToString()));
        Assert.Equal(DurationFormat.HoursMinutes, DurationFormatter.Parse(DurationFormat.HoursMinutes.ToString()));
    }
}
