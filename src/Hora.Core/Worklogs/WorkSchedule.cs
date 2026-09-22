using System.Text.Json.Serialization;

namespace Hora.Core.Worklogs;

/// <summary>Sollstunden je Wochentag. Wird unverschlüsselt in settings.json abgelegt.</summary>
public sealed class WorkSchedule
{
    public double Monday { get; set; }

    public double Tuesday { get; set; }

    public double Wednesday { get; set; }

    public double Thursday { get; set; }

    public double Friday { get; set; }

    public double Saturday { get; set; }

    public double Sunday { get; set; }

    public static WorkSchedule Default() => new()
    {
        Monday = 8,
        Tuesday = 8,
        Wednesday = 8,
        Thursday = 8,
        Friday = 8,
    };

    /// <summary>Kleinster und größter Wert, den der Schrittschalter der Profilseite zulässt.</summary>
    public const double MinWeekdayHours = 0;

    public const double MaxWeekdayHours = 24;

    /// <summary>
    /// Die Woche von Montag bis Sonntag. Die sieben Umschalter „Arbeitstage“ der Profilseite stehen
    /// in genau dieser Reihenfolge; die Aufzählung DayOfWeek beginnt dagegen am Sonntag.
    /// </summary>
    public static readonly IReadOnlyList<DayOfWeek> WeekOrder = new[]
    {
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
        DayOfWeek.Sunday,
    };

    /// <summary>Der eingetragene Wert eines Tages, unverändert — auch ein unsinniger aus der Datei.</summary>
    public double GetDayHours(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => Monday,
        DayOfWeek.Tuesday => Tuesday,
        DayOfWeek.Wednesday => Wednesday,
        DayOfWeek.Thursday => Thursday,
        DayOfWeek.Friday => Friday,
        DayOfWeek.Saturday => Saturday,
        _ => Sunday,
    };

    /// <summary>Setzt einen einzelnen Tag; der Wert wird auf 0 bis 24 begrenzt.</summary>
    public void SetDayHours(DayOfWeek day, double hours)
    {
        var clamped = Math.Clamp(hours, MinWeekdayHours, MaxWeekdayHours);

        switch (day)
        {
            case DayOfWeek.Monday: Monday = clamped; break;
            case DayOfWeek.Tuesday: Tuesday = clamped; break;
            case DayOfWeek.Wednesday: Wednesday = clamped; break;
            case DayOfWeek.Thursday: Thursday = clamped; break;
            case DayOfWeek.Friday: Friday = clamped; break;
            case DayOfWeek.Saturday: Saturday = clamped; break;
            default: Sunday = clamped; break;
        }
    }

    /// <summary>
    /// Als Arbeitstag gilt jeder Tag mit einem Wert größer null. Die sieben Umschalter der
    /// Profilseite lesen ihren Anfangszustand genau hier ab.
    /// </summary>
    public bool IsWorkday(DayOfWeek day) => GetDayHours(day) > 0;

    /// <summary>Zahl der Arbeitstage der Woche; null, wenn alle Tage abgewählt sind.</summary>
    /// <remarks>
    /// Nicht in die Datei: Dieser Typ wird als Teil von AppSettings serialisiert, und ein
    /// abgeleiteter Wert hat in settings.json nichts zu suchen — er stünde dort als „workdayCount“
    /// neben den sieben echten Werten und wäre beim nächsten Lesen sofort veraltet.
    /// </remarks>
    [JsonIgnore]
    public int WorkdayCount => WeekOrder.Count(IsWorkday);

    /// <summary>
    /// Anzeigewert des Schrittschalters „Sollstunden pro Tag“: die Sollzeit des ersten Arbeitstags
    /// der Woche (Montag bis Sonntag), der nicht null ist; sonst null. Anders als
    /// <see cref="GetWeekdayHours"/> sieht diese Methode auch eine Woche, die nur aus Samstag und
    /// Sonntag besteht — seit die Arbeitstage frei wählbar sind, kann es die geben. Sie liest nur
    /// und verändert nichts.
    /// </summary>
    public double GetWorkdayHours() => WeekOrder.Select(GetDayHours).FirstOrDefault(hours => hours > 0);

    /// <summary>
    /// Anzeigewert für den Schrittschalter „Sollstunden pro Tag“: die Sollzeit des ersten Werktags
    /// (Montag bis Freitag), der nicht null ist; sonst null. Wer in settings.json ungleiche Tage
    /// einträgt, behält sie — diese Methode liest nur und verändert nichts.
    /// </summary>
    public double GetWeekdayHours() =>
        new[] { Monday, Tuesday, Wednesday, Thursday, Friday }.FirstOrDefault(hours => hours > 0);

    /// <summary>
    /// Setzt Montag bis Freitag gleichmäßig auf <paramref name="hours"/>; Samstag und Sonntag
    /// werden null. Der Wert wird auf 0 bis 24 begrenzt.
    /// </summary>
    /// <remarks>
    /// Die feste Fünftagewoche. Seit die Arbeitstage frei wählbar sind, benutzt die Profilseite
    /// diesen Weg nicht mehr: Ihr Schrittschalter setzt über <see cref="SetDayHours"/> genau die
    /// ausgewählten Tage und lässt jeden abgewählten Tag auf null stehen.
    /// </remarks>
    public void SetWeekdayHours(double hours)
    {
        var clamped = Math.Clamp(hours, MinWeekdayHours, MaxWeekdayHours);

        Monday = clamped;
        Tuesday = clamped;
        Wednesday = clamped;
        Thursday = clamped;
        Friday = clamped;
        Saturday = 0;
        Sunday = 0;
    }

    public TimeSpan HoursFor(DayOfWeek day)
    {
        // Negative oder unsinnige Werte aus einer von Hand bearbeiteten Datei zählen als null.
        var hours = GetDayHours(day);
        return hours > 0 ? TimeSpan.FromHours(hours) : TimeSpan.Zero;
    }
}
