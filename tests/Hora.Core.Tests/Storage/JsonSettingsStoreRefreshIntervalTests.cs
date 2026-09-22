using Hora.Core.Storage;

namespace Hora.Core.Tests.Storage;

/// <summary>
/// Das Feld <c>refreshIntervalMinutes</c> in settings.json: der Abstand, in dem die Übersicht die
/// Buchungen von selbst nachholt. Null bedeutet aus. Eine Datei aus der Zeit davor kennt das Feld
/// nicht und muss deshalb mit fünf Minuten gelesen werden — nicht mit null, sonst schaltete ein
/// Programmstand-Wechsel die Aktualisierung stillschweigend ab.
/// </summary>
public sealed class JsonSettingsStoreRefreshIntervalTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HoraTests", Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;
    private readonly JsonSettingsStore _store;

    public JsonSettingsStoreRefreshIntervalTests()
    {
        _paths = new AppPaths(_root);
        _store = new JsonSettingsStore(_paths);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void New_settings_refresh_every_five_minutes()
    {
        Assert.Equal(5, new AppSettings().RefreshIntervalMinutes);
    }

    [Fact]
    public void The_chosen_interval_survives_a_restart()
    {
        _store.Save(new AppSettings { RefreshIntervalMinutes = 30 });

        Assert.Equal(30, _store.Load().RefreshIntervalMinutes);
    }

    [Fact]
    public void Switching_it_off_survives_a_restart()
    {
        // Null ist der Standardwert eines int — es muss trotzdem in der Datei stehen und
        // beim Lesen null bleiben, sonst schaltete sich die Aktualisierung von selbst wieder ein.
        _store.Save(new AppSettings { RefreshIntervalMinutes = 0 });

        Assert.Equal(0, _store.Load().RefreshIntervalMinutes);
    }

    [Fact]
    public void A_file_from_before_the_setting_is_read_with_five_minutes()
    {
        _paths.EnsureRootExists();
        File.WriteAllText(_paths.SettingsFile, """
            {
              "baseUrl": "https://advisori-ftc.atlassian.net",
              "email": "simon.strubel@advisori.de",
              "theme": "Dark"
            }
            """);

        Assert.Equal(5, _store.Load().RefreshIntervalMinutes);
    }
}
