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

    /// <summary>
    /// Schalter „Tickets aufschlüsseln“ des Export-Abschnitts. Die Vorgabe steht wie bei
    /// RefreshIntervalMinutes am Feld und nicht im Ladevorgang: Eine Datei aus der Zeit vor dem
    /// Export kennt den Schlüssel nicht, und ohne die Vorgabe stünde dort das false eines bool —
    /// ein Programmstand-Wechsel nähme dem Export also stillschweigend die Ticketzeilen.
    /// </summary>
    public bool ExportIncludeIssues { get; set; } = true;

    /// <summary>Schalter „Soll und Differenz“ des Export-Abschnitts; Vorgabe aus demselben Grund an.</summary>
    public bool ExportIncludeTargets { get; set; } = true;
}
