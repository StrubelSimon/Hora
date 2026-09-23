using Hora.Core.Worklogs;

namespace Hora.Core.Tests.Worklogs;

public class WeekCalendarTests
{
    [Theory]
    [InlineData(2026, 9, 14, 14)] // Montag bleibt Montag
    [InlineData(2026, 9, 16, 14)] // Mittwoch
    [InlineData(2026, 9, 20, 14)] // Sonntag gehört noch zur Woche vom 14.
    [InlineData(2026, 9, 21, 21)] // nächster Montag
    public void StartOfWeek_returns_monday_of_that_week(int year, int month, int day, int expectedDay)
    {
        var result = WeekCalendar.StartOfWeek(new DateOnly(year, month, day));

        Assert.Equal(new DateOnly(year, month, expectedDay), result);
        Assert.Equal(DayOfWeek.Monday, result.DayOfWeek);
    }

    [Fact]
    public void IsoWeekNumber_for_16th_september_2026_is_38()
    {
        Assert.Equal(38, WeekCalendar.IsoWeekNumber(new DateOnly(2026, 9, 16)));
    }

    [Fact]
    public void IsoWeekNumber_for_new_year_2027_belongs_to_week_53_of_2026()
    {
        // 1.1.2027 ist ein Freitag; die ISO-Woche 1 beginnt erst am 4.1.2027.
        Assert.Equal(53, WeekCalendar.IsoWeekNumber(new DateOnly(2027, 1, 1)));
    }

    [Fact]
    public void DayLabel_uses_german_weekday_and_full_date()
    {
        Assert.Equal("Mittwoch, 16.09.2026", WeekCalendar.DayLabel(new DateOnly(2026, 9, 16)));
    }

    [Fact]
    public void ShortDayLabel_uses_two_letter_weekday()
    {
        Assert.Equal("Mo 14.09.", WeekCalendar.ShortDayLabel(new DateOnly(2026, 9, 14)));
        Assert.Equal("So 20.09.", WeekCalendar.ShortDayLabel(new DateOnly(2026, 9, 20)));
    }

    [Fact]
    public void WeekLabel_shows_iso_week_and_monday_to_sunday()
    {
        Assert.Equal("KW 38 · 14.09. – 20.09.2026", WeekCalendar.WeekLabel(new DateOnly(2026, 9, 16)));
    }

    [Fact]
    public void ShortDayName_returns_the_two_letter_weekday_without_a_date()
    {
        Assert.Equal("Mo", WeekCalendar.ShortDayName(new DateOnly(2026, 9, 14)));
        Assert.Equal("Sa", WeekCalendar.ShortDayName(new DateOnly(2026, 9, 19)));
        Assert.Equal("So", WeekCalendar.ShortDayName(new DateOnly(2026, 9, 20)));
    }

    [Fact]
    public void MonthLabel_uses_the_german_month_name_and_the_year()
    {
        Assert.Equal("September 2026", WeekCalendar.MonthLabel(new DateOnly(2026, 9, 16)));
        Assert.Equal("Januar 2027", WeekCalendar.MonthLabel(new DateOnly(2027, 1, 31)));
        Assert.Equal("März 2026", WeekCalendar.MonthLabel(new DateOnly(2026, 3, 1)));
    }
}
