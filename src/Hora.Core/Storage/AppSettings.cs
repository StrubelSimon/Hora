using Hora.Core.Worklogs;

namespace Hora.Core.Storage;

/// <summary>Unverschlüsselte Einstellungen zum Vorbelegen des Login-Formulars.</summary>
public sealed class AppSettings
{
    public const string DefaultBaseUrl = "https://advisori-ftc.atlassian.net";

    public string BaseUrl { get; set; } = DefaultBaseUrl;

    public string Email { get; set; } = string.Empty;

    public WorkSchedule Schedule { get; set; } = WorkSchedule.Default();

    public string Theme { get; set; } = "Light";

    /// <summary>Standardwerte der Felder, die die Profilseite bedient.</summary>
    public const string DefaultStartView = "Day";

    public const string DefaultTimeFormat = "HoursMinutes";

    public const int DefaultRefreshIntervalMinutes = 5;

    /// <summary>
    /// Freie Rollenbezeichnung für das Abzeichen der Profilseite. Jira liefert so etwas nicht;
    /// der Wert steht nur in dieser Datei. Leer bedeutet: Das Abzeichen wird ausgeblendet.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Ansicht beim Programmstart: „Day“, „Week“ oder „Month“.</summary>
    public string StartView { get; set; } = DefaultStartView;

    /// <summary>Darstellung aller Dauern: „HoursMinutes“ oder „DecimalHours“.</summary>
    public string TimeFormat { get; set; } = DefaultTimeFormat;

    /// <summary>
    /// Merkt nur die Wahl des Nutzers. Eine tatsächliche Benachrichtigung baut die App bewusst
    /// nicht; sie liefe ohnehin nur, solange das Programm offen ist.
    /// </summary>
    public bool ReminderEnabled { get; set; }

    /// <summary>
    /// Abstand, in dem die Übersicht die Buchungen von selbst nachholt, in Minuten; null bedeutet
    /// aus. Die Profilseite bietet 0, 5, 15 und 30 an.
    /// </summary>
    /// <remarks>
    /// Die Vorgabe steht am Feld, nicht im Ladevorgang: Eine Datei aus der Zeit vor dieser
    /// Einstellung kennt den Schlüssel nicht, und ohne die Vorgabe stünde dort die Null eines int —
    /// ein Programmstand-Wechsel schaltete die Aktualisierung also stillschweigend ab.
    /// </remarks>
    public int RefreshIntervalMinutes { get; set; } = DefaultRefreshIntervalMinutes;

    /// <summary>Zeitpunkt des letzten erfolgreichen Abrufs; null, solange es keinen gab.</summary>
    public DateTimeOffset? LastSyncAt { get; set; }

    /// <summary>Zeitpunkt des letzten erfolgreichen Asana-Abrufs; null, solange es keinen gab.</summary>
    public DateTimeOffset? AsanaLastSyncAt { get; set; }
}
