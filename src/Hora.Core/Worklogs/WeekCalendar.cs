using System.Globalization;

namespace Hora.Core.Worklogs;

/// <summary>Kalenderhilfen: Wochenstart am Montag, ISO-Kalenderwoche, deutsche Beschriftungen.</summary>
public static class WeekCalendar
{
    // Feste deutsche Namen, damit die Anzeige nicht von der Systemsprache abhängt. Index = DayOfWeek (Sonntag = 0).
    private static readonly string[] LongDayNames =
        { "Sonntag", "Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag", "Samstag" };

    private static readonly string[] ShortDayNames =
        { "So", "Mo", "Di", "Mi", "Do", "Fr", "Sa" };

    public static DateOnly StartOfWeek(DateOnly date)
    {
        // DayOfWeek zählt ab Sonntag = 0; wir wollen Montag = 0.
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }

    public static int IsoWeekNumber(DateOnly date) =>
        ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));

    public static string DayLabel(DateOnly date) =>
        $"{LongDayNames[(int)date.DayOfWeek]}, {date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}";

    public static string ShortDayLabel(DateOnly date) =>
        $"{ShortDayNames[(int)date.DayOfWeek]} {date.ToString("dd.MM.", CultureInfo.InvariantCulture)}";

    public static string WeekLabel(DateOnly anyDayInWeek)
    {
        var start = StartOfWeek(anyDayInWeek);
        var end = start.AddDays(6);
        return $"KW {IsoWeekNumber(start)} · {start.ToString("dd.MM.", CultureInfo.InvariantCulture)} – {end.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}";
    }
}
