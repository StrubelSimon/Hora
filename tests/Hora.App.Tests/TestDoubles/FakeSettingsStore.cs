using Hora.Core.Storage;
using Hora.Core.Worklogs;

namespace Hora.App.Tests.TestDoubles;

/// <summary>
/// Attrappe für <see cref="ISettingsStore"/>. Load und Save geben bewusst jeweils eine Kopie
/// weiter, genau wie der echte JsonSettingsStore, der über die Datei geht: Gäbe Load dasselbe
/// Objekt heraus, das Save später ablegt, würde schon das bloße Verändern des geladenen Objekts
/// wie ein erfolgreiches Speichern aussehen — und ein Test könnte nicht mehr unterscheiden, ob
/// wirklich gespeichert wurde (Befund A).
/// </summary>
internal sealed class FakeSettingsStore : ISettingsStore
{
    private AppSettings _settings = new();

    public AppSettings Settings
    {
        get => _settings;
        set => _settings = Copy(value);
    }

    public int SaveCalls { get; private set; }

    public AppSettings Load() => Copy(_settings);

    public void Save(AppSettings settings)
    {
        _settings = Copy(settings);
        SaveCalls++;
    }

    private static AppSettings Copy(AppSettings settings) => new()
    {
        BaseUrl = settings.BaseUrl,
        Email = settings.Email,
        Theme = settings.Theme,
        // Die Felder der Profilseite gehören mit in die Kopie; ohne sie fiele jede gespeicherte
        // Einstellung beim nächsten Load wieder auf den Standardwert zurück.
        Role = settings.Role,
        StartView = settings.StartView,
        TimeFormat = settings.TimeFormat,
        ReminderEnabled = settings.ReminderEnabled,
        RefreshIntervalMinutes = settings.RefreshIntervalMinutes,
        LastSyncAt = settings.LastSyncAt,
        Schedule = settings.Schedule is null
            ? null!
            : new WorkSchedule
            {
                Monday = settings.Schedule.Monday,
                Tuesday = settings.Schedule.Tuesday,
                Wednesday = settings.Schedule.Wednesday,
                Thursday = settings.Schedule.Thursday,
                Friday = settings.Schedule.Friday,
                Saturday = settings.Schedule.Saturday,
                Sunday = settings.Schedule.Sunday,
            },
    };
}
