using Hora.Core.Storage;
using Hora.Core.Worklogs;

namespace Hora.Core.Tests.Storage;

/// <summary>
/// Der Abschnitt <c>schedule</c> in settings.json. In der laufenden App aufgefallen: Eine abgeleitete
/// Eigenschaft des Modells landete als zusätzliches Feld in der Datei. Dort hat sie nichts zu
/// suchen — sie wäre beim nächsten Lesen sofort veraltet und verwirrt jeden, der die Datei von Hand
/// bearbeitet.
/// </summary>
public sealed class JsonSettingsStoreScheduleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HoraTests", Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;
    private readonly JsonSettingsStore _store;

    public JsonSettingsStoreScheduleTests()
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
    public void The_file_holds_the_seven_days_and_nothing_derived()
    {
        _store.Save(new AppSettings { Schedule = WorkSchedule.Default() });

        var json = File.ReadAllText(_paths.SettingsFile);

        foreach (var day in new[] { "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday" })
        {
            Assert.Contains($"\"{day}\"", json);
        }

        Assert.DoesNotContain("workdayCount", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("weekOrder", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_file_with_an_unknown_extra_field_is_still_read()
    {
        // Eine Datei, die eine frühere Fassung mit dem abgeleiteten Feld geschrieben hat, muss
        // weiterhin lesbar sein.
        Directory.CreateDirectory(_root);
        File.WriteAllText(
            _paths.SettingsFile,
            "{ \"schedule\": { \"monday\": 6, \"tuesday\": 6, \"workdayCount\": 2 } }");

        var settings = _store.Load();

        Assert.Equal(6, settings.Schedule.Monday);
        Assert.Equal(6, settings.Schedule.Tuesday);
        Assert.Equal(0, settings.Schedule.Sunday);
        Assert.Equal(2, settings.Schedule.WorkdayCount);
    }

    [Fact]
    public void A_week_of_freely_chosen_workdays_survives_a_restart()
    {
        var schedule = new WorkSchedule();
        schedule.SetDayHours(DayOfWeek.Tuesday, 7.5);
        schedule.SetDayHours(DayOfWeek.Saturday, 7.5);
        _store.Save(new AppSettings { Schedule = schedule });

        var reloaded = new JsonSettingsStore(_paths).Load().Schedule;

        Assert.Equal(7.5, reloaded.Tuesday);
        Assert.Equal(7.5, reloaded.Saturday);
        Assert.Equal(2, reloaded.WorkdayCount);
        Assert.True(reloaded.IsWorkday(DayOfWeek.Saturday));
        Assert.False(reloaded.IsWorkday(DayOfWeek.Monday));
    }
}
