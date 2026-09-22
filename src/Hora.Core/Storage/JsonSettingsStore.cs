using System.Text.Json;
using Hora.Core.Worklogs;

namespace Hora.Core.Storage;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly AppPaths _paths;

    public JsonSettingsStore(AppPaths paths)
    {
        _paths = paths;
    }

    public AppSettings Load()
    {
        if (!File.Exists(_paths.SettingsFile))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_paths.SettingsFile);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();

            // Sichere null-Werte ab, die durch Handbearbeitung entstanden sein könnten.
            settings.BaseUrl = string.IsNullOrEmpty(settings.BaseUrl) ? AppSettings.DefaultBaseUrl : settings.BaseUrl;
            settings.Email = string.IsNullOrEmpty(settings.Email) ? string.Empty : settings.Email;
            settings.Schedule ??= WorkSchedule.Default();
            settings.Theme = string.IsNullOrEmpty(settings.Theme) ? "Light" : settings.Theme;

            // Dieselbe Absicherung für die Felder der Profilseite. Eine ältere Datei kennt sie gar
            // nicht (dann greifen die Vorgaben der Eigenschaften), eine von Hand bearbeitete kann
            // sie auf null setzen — dann stünde hier ohne diese Zeilen null in einem string.
            settings.Role ??= string.Empty;
            settings.StartView = string.IsNullOrEmpty(settings.StartView) ? AppSettings.DefaultStartView : settings.StartView;
            settings.TimeFormat = string.IsNullOrEmpty(settings.TimeFormat) ? AppSettings.DefaultTimeFormat : settings.TimeFormat;

            return settings;
        }
        catch (Exception)
        {
            // Unlesbar bedeutet Standardwerte — derselbe Vertrag wie beim Protokollschreiber. Der
            // Filter fing zuvor nur JsonException und IOException; eine UnauthorizedAccessException
            // (Ordnerumleitung auf eine Netzfreigabe, restriktive Zugriffsrechte) erbt dagegen von
            // SystemException und entkam. Sie fiel damit bis in App.OnStartup, also vor das Erzeugen
            // des Fensters, und ließ die App als unsichtbaren Prozess weiterlaufen (Befund C).
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        _paths.EnsureRootExists();
        File.WriteAllText(_paths.SettingsFile, JsonSerializer.Serialize(settings, Options));
    }
}
