using Hora.Core.Storage;

namespace Hora.App.ViewModels;

/// <summary>
/// Die Abstände, die die Profilseite für die automatische Aktualisierung anbietet, und ihre
/// Beschriftung. Unbekanntes — etwa aus einer von Hand bearbeiteten settings.json — ergibt den
/// Standardabstand; eine verdorbene Datei darf den Takt nicht kosten. Dasselbe Vorgehen wie bei
/// <see cref="ViewModeNames"/>.
/// </summary>
public static class RefreshIntervals
{
    public const int Off = 0;

    public const int Default = AppSettings.DefaultRefreshIntervalMinutes;

    /// <summary>Die vier Segmente der Zeile „Automatisch aktualisieren“, in der Reihenfolge der Anzeige.</summary>
    public static readonly IReadOnlyList<int> Allowed = new[] { Off, 5, 15, 30 };

    public static int Parse(int storedMinutes) => Allowed.Contains(storedMinutes) ? storedMinutes : Default;

    /// <summary>Unterzeile der Zeile „Automatisch aktualisieren“.</summary>
    public static string TextFor(int minutes) => minutes == Off ? "Ausgeschaltet" : $"Alle {minutes} Minuten";
}
