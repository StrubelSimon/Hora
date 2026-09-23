namespace Hora.App.ViewModels;

/// <summary>
/// Die drei Abschnitte eines Balkens als Anteile seiner vollen Breite: grün bis zum Tagessoll,
/// orange für die Mehrzeit darüber, rot ab der gesetzlichen Höchstarbeitszeit. Die Summe der drei
/// Anteile ist nie größer als eins; der fehlende Rest bleibt unausgefüllte Spur.
/// </summary>
/// <remarks>
/// Bezugsgröße der Anteile ist der größere Wert aus Soll und erfasster Zeit. Unterhalb des Solls
/// misst der Balken damit wie eh und je am Soll (ein halber Tag füllt ihn halb); ab dem Soll ist er
/// voll und teilt sich in die Farbabschnitte auf. „Voll“ heißt also weiterhin „Soll erreicht“, und
/// wie weit jemand darüber liegt, zeigt der Anteil von Orange und Rot.
/// </remarks>
public sealed record BarSegments(double TargetShare, double OvertimeShare, double OverLimitShare)
{
    public static readonly BarSegments Empty = new(0.0, 0.0, 0.0);

    /// <summary>
    /// <paramref name="limit"/> ist die Höchstarbeitszeit des Tages (§ 3 ArbZG: zehn Stunden).
    /// Ohne Grenze – für Wochen- und Monatssummen, die keine solche Tagesregel kennen – bleibt der
    /// rote Abschnitt leer und alles über dem Soll ist Mehrzeit.
    /// </summary>
    public static BarSegments For(TimeSpan logged, TimeSpan target, TimeSpan? limit = null)
    {
        // Bezugsgröße: das Größere aus Soll und erfasster Zeit. Sind beide null – ein leerer Tag
        // ohne Soll –, gibt es nichts zu zeichnen und auch nichts zu teilen.
        var scale = target > logged ? target : logged;
        if (scale <= TimeSpan.Zero)
        {
            return Empty;
        }

        // Rot sticht Grün: Wer zwölf Stunden Soll einträgt, reißt die gesetzliche Grenze trotzdem ab
        // der zehnten Stunde. Deshalb endet der grüne Abschnitt beim kleineren Wert aus Soll und
        // Grenze, und der orange Abschnitt ist genau das, was zwischen beiden übrig bleibt.
        var green = Min(logged, target, limit);
        var red = limit is { } cap && logged > cap ? logged - cap : TimeSpan.Zero;
        var orange = logged - green - red;

        return new BarSegments(green / scale, orange / scale, red / scale);
    }

    private static TimeSpan Min(TimeSpan logged, TimeSpan target, TimeSpan? limit)
    {
        var smallest = logged < target ? logged : target;
        return limit is { } cap && cap < smallest ? cap : smallest;
    }
}
