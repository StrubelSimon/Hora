using Hora.Core.Storage;

namespace Hora.Core.Tests.Storage;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HoraTests", Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;
    private readonly JsonSettingsStore _store;

    public JsonSettingsStoreTests()
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
    public void Load_returns_defaults_when_no_file_exists()
    {
        var settings = _store.Load();

        Assert.Equal(AppSettings.DefaultBaseUrl, settings.BaseUrl);
        Assert.Equal(string.Empty, settings.Email);
    }

    [Fact]
    public void Save_then_Load_roundtrips_settings()
    {
        _store.Save(new AppSettings { BaseUrl = "https://other.atlassian.net", Email = "x@y.de" });

        var loaded = _store.Load();

        Assert.Equal("https://other.atlassian.net", loaded.BaseUrl);
        Assert.Equal("x@y.de", loaded.Email);
    }

    [Fact]
    public void Load_returns_defaults_for_corrupt_file()
    {
        _paths.EnsureRootExists();
        File.WriteAllText(_paths.SettingsFile, "{ kaputt");

        var settings = _store.Load();

        Assert.Equal(AppSettings.DefaultBaseUrl, settings.BaseUrl);
    }

    [Fact]
    public void Load_returns_defaults_when_the_file_cannot_be_read()
    {
        // Befund C: Der Filter fing nur JsonException und IOException. Eine
        // UnauthorizedAccessException erbt von SystemException und entkam bis in App.OnStartup —
        // also vor das Erzeugen des Fensters. Für diese Klasse gilt derselbe Vertrag wie für den
        // Protokollschreiber: unlesbar bedeutet Standardwerte, nicht Absturz.
        _paths.EnsureRootExists();
        File.WriteAllText(_paths.SettingsFile, """
            {
              "baseUrl": "https://gesperrt.atlassian.net",
              "email": "gesperrt@example.com",
              "theme": "Dark"
            }
            """);

        using var denial = FileReadDenial.For(_paths.SettingsFile);
        // Vorbedingung: Das Leserecht ist wirklich entzogen, der Test prüft also den gewollten Fall.
        Assert.Throws<UnauthorizedAccessException>(() => File.ReadAllText(_paths.SettingsFile));

        var settings = _store.Load();

        Assert.Equal(AppSettings.DefaultBaseUrl, settings.BaseUrl);
        Assert.Equal(string.Empty, settings.Email);
        Assert.Equal("Light", settings.Theme);
        Assert.Equal(TimeSpan.FromHours(8), settings.Schedule.HoursFor(DayOfWeek.Monday));
    }

    [Fact]
    public void Load_uses_defaults_for_missing_schedule_and_theme_fields()
    {
        _paths.EnsureRootExists();
        // Eine ältere settings.json ohne Schedule und Theme (mit Kleinschreibung wie die echte Datei)
        File.WriteAllText(_paths.SettingsFile, """
            {
              "baseUrl": "https://advisori-ftc.atlassian.net",
              "email": "test@example.com"
            }
            """);

        var settings = _store.Load();

        Assert.Equal("https://advisori-ftc.atlassian.net", settings.BaseUrl);
        Assert.Equal("test@example.com", settings.Email);
        // Schedule und Theme sollten die Standardwerte haben
        Assert.Equal(TimeSpan.FromHours(8), settings.Schedule.HoursFor(DayOfWeek.Monday));
        Assert.Equal(TimeSpan.Zero, settings.Schedule.HoursFor(DayOfWeek.Saturday));
        Assert.Equal("Light", settings.Theme);
    }

    [Fact]
    public void Load_handles_null_schedule()
    {
        _paths.EnsureRootExists();
        File.WriteAllText(_paths.SettingsFile, """
            {
              "baseUrl": "https://advisori-ftc.atlassian.net",
              "email": "test@example.com",
              "schedule": null,
              "theme": "Dark"
            }
            """);

        var settings = _store.Load();

        // Schedule sollte Default sein, nicht null
        Assert.Equal(TimeSpan.FromHours(8), settings.Schedule.HoursFor(DayOfWeek.Monday));
        Assert.Equal("Dark", settings.Theme);
    }

    [Fact]
    public void Load_handles_null_theme()
    {
        _paths.EnsureRootExists();
        File.WriteAllText(_paths.SettingsFile, """
            {
              "baseUrl": "https://advisori-ftc.atlassian.net",
              "email": "test@example.com",
              "schedule": { "monday": 8, "tuesday": 8, "wednesday": 8, "thursday": 8, "friday": 8 },
              "theme": null
            }
            """);

        var settings = _store.Load();

        // Theme sollte "Light" sein, nicht null
        Assert.Equal("Light", settings.Theme);
    }

    [Fact]
    public void Load_handles_empty_theme()
    {
        _paths.EnsureRootExists();
        File.WriteAllText(_paths.SettingsFile, """
            {
              "baseUrl": "https://advisori-ftc.atlassian.net",
              "email": "test@example.com",
              "theme": ""
            }
            """);

        var settings = _store.Load();

        // Leeres Theme sollte zu "Light" werden
        Assert.Equal("Light", settings.Theme);
    }

    [Fact]
    public void Load_handles_null_baseUrl()
    {
        _paths.EnsureRootExists();
        File.WriteAllText(_paths.SettingsFile, """
            {
              "baseUrl": null,
              "email": "test@example.com"
            }
            """);

        var settings = _store.Load();

        // BaseUrl sollte DefaultBaseUrl sein, nicht null
        Assert.Equal(AppSettings.DefaultBaseUrl, settings.BaseUrl);
    }

    [Fact]
    public void Load_handles_null_email()
    {
        _paths.EnsureRootExists();
        File.WriteAllText(_paths.SettingsFile, """
            {
              "baseUrl": "https://advisori-ftc.atlassian.net",
              "email": null
            }
            """);

        var settings = _store.Load();

        // Email sollte leer sein, nicht null
        Assert.Equal(string.Empty, settings.Email);
    }
}
