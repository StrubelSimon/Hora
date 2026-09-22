using System.Globalization;

namespace Hora.Core.Worklogs;

/// <summary>
/// Formatiert Dauern, entweder im Jira-Stil („7h 45m“) oder als Dezimalstunden („7,75h“).
/// Die Wahl kommt als Parameter herein und hat bewusst einen Standardwert: Der Formatierer bleibt
/// damit zustandslos, und die vielen vorhandenen Aufrufstellen ohne Format behalten ihr Verhalten.
/// </summary>
public static class DurationFormatter
{
    // Feste Kultur statt der Kultur des Prozesses: Das Komma der Dezimaldarstellung ist eine
    // Vorgabe des Entwurfs, keine Eigenschaft der Windows-Ländereinstellung.
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    public static string Format(TimeSpan duration, DurationFormat format = DurationFormat.HoursMinutes)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        // Sekunden werden abgeschnitten, nicht gerundet.
        var totalMinutes = (long)Math.Floor(duration.TotalMinutes);

        return format == DurationFormat.DecimalHours
            ? FormatDecimalHours(totalMinutes)
            : FormatHoursAndMinutes(totalMinutes);
    }

    /// <summary>
    /// Liest den in settings.json abgelegten Namen. Alles Unbekannte — auch null und leer — ergibt
    /// die bisherige Darstellung; eine von Hand verdorbene Datei darf die Anzeige nicht kosten.
    /// </summary>
    public static DurationFormat Parse(string? storedName) =>
        Enum.TryParse<DurationFormat>(storedName, ignoreCase: true, out var format)
            ? format
            : DurationFormat.HoursMinutes;

    private static string FormatHoursAndMinutes(long totalMinutes)
    {
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;

        if (hours == 0)
        {
            return $"{minutes}m";
        }

        return minutes == 0 ? $"{hours}h" : $"{hours}h {minutes}m";
    }

    /// <summary>
    /// Zwei Nachkommastellen mit Komma; glatte Stunden ohne Nachkommastellen („8h“, nicht „8,00h“).
    /// </summary>
    private static string FormatDecimalHours(long totalMinutes)
    {
        if (totalMinutes % 60 == 0)
        {
            return $"{totalMinutes / 60}h";
        }

        // Ab hier bleibt ein Rest von 1 bis 59 Minuten. Der kleinste davon ist eine Minute, also
        // rund 0,017 Stunden — mehr als die 0,005, die das Runden auf zwei Stellen verschluckt.
        // Eine glatte Stunde kann hier deshalb nicht mehr herauskommen.
        var hours = Math.Round(totalMinutes / 60.0, 2, MidpointRounding.AwayFromZero);
        return $"{hours.ToString("0.00", German)}h";
    }
}
