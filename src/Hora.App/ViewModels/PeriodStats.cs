using Hora.Core.Worklogs;

namespace Hora.App.ViewModels;

/// <summary>
/// Kennzahlen eines Zeitraums: erfasste Zeit gegen Sollzeit, mit fertig formatierten Texten für die
/// Kennzahlenkarte (Abschnitt 3.3 der Gestaltungsvorgaben).
/// </summary>
/// <remarks>
/// <paramref name="Format"/> steht als Bestandteil des Datensatzes hier und nicht in einem statischen
/// Zustand: Die Kennzahlenkarte bindet auf die fertigen Texte, und ein Wechsel des Zeitformats erzeugt
/// einen neuen Datensatz statt still einen globalen Schalter umzulegen.
/// </remarks>
public sealed record PeriodStats(
    TimeSpan Logged,
    TimeSpan Target,
    TimeSpan Difference,
    double Ratio,
    DurationFormat Format = DurationFormat.HoursMinutes,
    TimeSpan? Limit = null)
{
    /// <summary>
    /// <paramref name="limit"/> ist die Höchstarbeitszeit des Zeitraums, jenseits derer der Balken
    /// rot wird. Gesetzt wird sie nur in der Tagesansicht (zehn Stunden, § 3 ArbZG); Wochen- und
    /// Monatssumme bleiben ohne, denn die Grenze gilt je Tag und nicht je Zeitraum.
    /// </summary>
    public static PeriodStats For(
        TimeSpan logged,
        TimeSpan target,
        DurationFormat format = DurationFormat.HoursMinutes,
        TimeSpan? limit = null)
    {
        var ratio = target > TimeSpan.Zero ? logged.TotalHours / target.TotalHours : 0.0;
        return new PeriodStats(logged, target, logged - target, ratio, format, limit);
    }

    public string LoggedText => DurationFormatter.Format(Logged, Format);

    public string TargetText => DurationFormatter.Format(Target, Format);

    public bool IsNegative => Difference < TimeSpan.Zero;

    /// <summary>Exakt null wird ohne Vorzeichen als "0h" gezeigt; negative Werte mit echtem Minuszeichen "−".</summary>
    public string DifferenceText
    {
        get
        {
            if (Difference == TimeSpan.Zero)
            {
                return "0h";
            }

            var magnitude = DurationFormatter.Format(IsNegative ? Difference.Negate() : Difference, Format);
            return IsNegative ? $"−{magnitude}" : $"+{magnitude}";
        }
    }

    /// <summary>Ist das Soll null (z. B. am Wochenende), zeigt der Prozenttext "–" statt eines Werts.</summary>
    public string PercentText => Target > TimeSpan.Zero
        ? $"{Math.Round(Ratio * 100, MidpointRounding.AwayFromZero):0} %"
        : "–";

    /// <summary>
    /// Die drei Abschnitte des Balkens: grün bis zum Soll, orange darüber, rot jenseits der Grenze.
    /// Anders als PercentText schießt er nie über die volle Länge hinaus – wie weit jemand über dem
    /// Soll liegt, zeigt stattdessen der Anteil der farbigen Abschnitte.
    /// </summary>
    public BarSegments Segments => BarSegments.For(Logged, Target, Limit);
}
