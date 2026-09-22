using Hora.Core.Storage;

namespace Hora.Core.Tests.Storage;

/// <summary>
/// Die fünf Felder der Profilseite in settings.json. Zwei Dinge waren hier schon einmal Fehler und
/// werden deshalb ausdrücklich geprüft: Nullwerte einzelner Felder aus einer von Hand bearbeiteten
/// Datei, und eine ältere Datei ganz ohne die neuen Felder.
/// </summary>
public sealed class JsonSettingsStoreProfileFieldsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HoraTests", Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;
    private readonly JsonSettingsStore _store;

    public JsonSettingsStoreProfileFieldsTests()
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
    public void New_settings_carry_the_documented_defaults()
    {
        var settings = new AppSettings();

        Assert.Equal(string.Empty, settings.Role);
        Assert.Equal("Day", settings.StartView);
        Assert.Equal("HoursMinutes", settings.TimeFormat);
        Assert.False(settings.ReminderEnabled);
        Assert.Null(settings.LastSyncAt);
    }

    [Fact]
    public void An_older_file_without_the_new_fields_is_read_with_the_defaults()
    {
        // Genau die Datei, die der Auftraggeber im Einsatz hat: baseUrl, email, schedule, theme.
        _paths.EnsureRootExists();
        File.WriteAllText(_paths.SettingsFile, """
            {
              "baseUrl": "https://advisori-ftc.atlassian.net",
              "email": "simon.strubel@advisori.de",
              "schedule": {
                "monday": 8,
                "tuesday": 8,
                "wednesday": 8,
                "thursday": 8,
                "friday": 8,
                "saturday": 0,
                "sunday": 0
              },
              "theme": "Dark"
            }
            """);

        var settings = _store.Load();

        // Der Altbestand bleibt unangetastet …
        Assert.Equal("https://advisori-ftc.atlassian.net", settings.BaseUrl);
        Assert.Equal("simon.strubel@advisori.de", settings.Email);
        Assert.Equal("Dark", settings.Theme);
        Assert.Equal(TimeSpan.FromHours(8), settings.Schedule.HoursFor(DayOfWeek.Monday));
        // … und die neuen Felder bekommen ihre Standardwerte.
        Assert.Equal(string.Empty, settings.Role);
        Assert.Equal("Day", settings.StartView);
        Assert.Equal("HoursMinutes", settings.TimeFormat);
        Assert.False(settings.ReminderEnabled);
        Assert.Null(settings.LastSyncAt);
    }

    [Fact]
    public void Null_values_in_the_new_fields_fall_back_to_the_defaults()
    {
        _paths.EnsureRootExists();
        File.WriteAllText(_paths.SettingsFile, """
            {
              "baseUrl": "https://advisori-ftc.atlassian.net",
              "email": "test@example.com",
              "role": null,
              "startView": null,
              "timeFormat": null,
              "lastSyncAt": null
            }
            """);

        var settings = _store.Load();

        Assert.Equal(string.Empty, settings.Role);
        Assert.Equal("Day", settings.StartView);
        Assert.Equal("HoursMinutes", settings.TimeFormat);
        Assert.Null(settings.LastSyncAt);
    }

    [Fact]
    public void Empty_strings_in_the_new_fields_fall_back_to_the_defaults()
    {
        _paths.EnsureRootExists();
        File.WriteAllText(_paths.SettingsFile, """
            {
              "baseUrl": "https://advisori-ftc.atlassian.net",
              "email": "test@example.com",
              "startView": "",
              "timeFormat": ""
            }
            """);

        var settings = _store.Load();

        Assert.Equal("Day", settings.StartView);
        Assert.Equal("HoursMinutes", settings.TimeFormat);
    }

    [Fact]
    public void An_empty_role_stays_empty_because_the_badge_hides_on_it()
    {
        _paths.EnsureRootExists();
        File.WriteAllText(_paths.SettingsFile, """
            {
              "baseUrl": "https://advisori-ftc.atlassian.net",
              "role": ""
            }
            """);

        Assert.Equal(string.Empty, _store.Load().Role);
    }

    [Fact]
    public void Save_then_Load_roundtrips_the_new_fields()
    {
        var lastSync = new DateTimeOffset(2026, 9, 17, 9, 12, 0, TimeSpan.FromHours(2));

        _store.Save(new AppSettings
        {
            Role = "Senior Consultant",
            StartView = "Week",
            TimeFormat = "DecimalHours",
            ReminderEnabled = true,
            LastSyncAt = lastSync,
        });

        var loaded = _store.Load();

        Assert.Equal("Senior Consultant", loaded.Role);
        Assert.Equal("Week", loaded.StartView);
        Assert.Equal("DecimalHours", loaded.TimeFormat);
        Assert.True(loaded.ReminderEnabled);
        Assert.Equal(lastSync, loaded.LastSyncAt);
    }

    [Fact]
    public void Unequal_weekday_hours_from_a_hand_edited_file_survive_a_roundtrip()
    {
        _paths.EnsureRootExists();
        File.WriteAllText(_paths.SettingsFile, """
            {
              "baseUrl": "https://advisori-ftc.atlassian.net",
              "schedule": { "monday": 4, "tuesday": 8, "wednesday": 8, "thursday": 8, "friday": 6 }
            }
            """);

        var settings = _store.Load();
        _store.Save(settings);
        var reloaded = _store.Load();

        Assert.Equal(4, reloaded.Schedule.Monday);
        Assert.Equal(8, reloaded.Schedule.Tuesday);
        Assert.Equal(6, reloaded.Schedule.Friday);
    }
}
