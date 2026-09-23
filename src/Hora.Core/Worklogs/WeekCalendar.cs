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

    // Index = Monatsnummer; Feld 0 bleibt leer, damit der Monat direkt als Index taugt.
    private static readonly string[] MonthNames =
    {
        string.Empty,
        "Januar", "Februar", "März", "April", "Mai", "Juni",
        "Juli", "August", "September", "Oktober", "November", "Dezember",
    };

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

    /// <summary>Nur der Wochentag, zweibuchstabig („Mo“) — ohne Datum, für die Spalte des Exports.</summary>
    public static string ShortDayName(DateOnly date) => ShortDayNames[(int)date.DayOfWeek];

    /// <summary>Monat und Jahr, deutsch ausgeschrieben („September 2026“).</summary>
    public static string MonthLabel(DateOnly anyDayInMonth) =>
        $"{MonthNames[anyDayInMonth.Month]} {anyDayInMonth.Year}";

    public static string WeekLabel(DateOnly anyDayInWeek)
    {
        var start = StartOfWeek(anyDayInWeek);
        var end = start.AddDays(6);
        return $"KW {IsoWeekNumber(start)} · {start.ToString("dd.MM.", CultureInfo.InvariantCulture)} – {end.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}";
    }
}
