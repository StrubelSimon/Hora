using Hora.Core.Asana.Models;
using Hora.Core.Tests.Asana;
using Hora.Core.Worklogs;

namespace Hora.Core.Tests.Worklogs;

public class AsanaWorklogServiceTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    private static AsanaTimeEntry Entry(
        string gid = "1",
        int minutes = 90,
        string enteredOn = "2026-09-21",
        string? project = "Marketing",
        string? projectGid = "700",
        string? category = null,
        string? taskName = null)
    {
        return new AsanaTimeEntry
        {
            Gid = gid,
            DurationMinutes = minutes,
            EnteredOn = DateOnly.Parse(enteredOn),
            AttributableTo = project is null ? null : new AsanaProject { Gid = projectGid!, Name = project },
            Categories = category is null ? new() : new() { new AsanaTimeTrackingCategory { Gid = "800", Name = category } },
            Task = taskName is null ? null : new AsanaTask { Gid = "600", Name = taskName, PermalinkUrl = "https://app.asana.com/0/0/600" },
        };
    }

    private static AsanaWorklogService ServiceFor(params AsanaTimeEntry[] entries) =>
        new(new FakeAsanaClient(entries), "111", "900", Berlin);

    [Fact]
    public async Task MapsDurationSourceAndProjectBadge()
    {
        var result = await ServiceFor(Entry()).GetEntriesAsync(
            new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27), CancellationToken.None);

        var entry = Assert.Single(result.Entries);
        Assert.Equal(WorklogSource.Asana, entry.Source);
        Assert.Equal("Marketing", entry.ItemKey);
        Assert.Equal(TimeSpan.FromMinutes(90), entry.Duration);
        Assert.False(entry.HasStartTime);
        Assert.Equal("https://app.asana.com/0/700", entry.ItemUrl);
    }

    /// <summary>
    /// Der Zeitstempel ist lokale Mitternacht des gebuchten Tags. Nur so fällt der Eintrag beim
    /// Aggregieren auf denselben Kalendertag, den Asana meint — und der gesamte Apparat aus
    /// Tages-, Wochen- und Monatssummen läuft ohne Sonderzweig weiter.
    /// </summary>
    [Fact]
    public async Task StartsAtLocalMidnightOfTheEnteredDay()
    {
        var result = await ServiceFor(Entry(enteredOn: "2026-09-21")).GetEntriesAsync(
            new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27), CancellationToken.None);

        var entry = Assert.Single(result.Entries);
        Assert.Equal(new DateOnly(2026, 9, 21), entry.LocalDate(Berlin));
        var local = TimeZoneInfo.ConvertTime(entry.Started, Berlin);
        Assert.Equal(new TimeSpan(0, 0, 0), local.TimeOfDay);
    }

    [Fact]
    public async Task PassesUserWorkspaceAndRangeToTheClient()
    {
        var client = new FakeAsanaClient(new[] { Entry() });
        var service = new AsanaWorklogService(client, "111", "900", Berlin);

        await service.GetEntriesAsync(new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27), CancellationToken.None);

        Assert.Equal("111", client.LastUserGid);
        Assert.Equal("900", client.LastWorkspaceGid);
        Assert.Equal(new DateOnly(2026, 9, 21), client.LastFrom);
        Assert.Equal(new DateOnly(2026, 9, 27), client.LastTo);
    }

    /// <summary>
    /// Fall B aus Abschnitt 3.3 der Spezifikation: Ohne Task-Namen tritt die Kategorie an seine
    /// Stelle, sonst der Projektname. Die Zeile bleibt so in jedem Fall lesbar.
    /// </summary>
    [Fact]
    public async Task UsesCategoryAsTitleWhenTheTaskIsMissing()
    {
        var result = await ServiceFor(Entry(category: "Entwicklung")).GetEntriesAsync(
            new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27), CancellationToken.None);

        Assert.Equal("Entwicklung", Assert.Single(result.Entries).ItemSummary);
    }

    [Fact]
    public async Task FallsBackToProjectNameWithoutTaskOrCategory()
    {
        var result = await ServiceFor(Entry(category: null)).GetEntriesAsync(
            new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27), CancellationToken.None);

        Assert.Equal("Marketing", Assert.Single(result.Entries).ItemSummary);
    }

    [Fact]
    public async Task UsesTaskNameAndPermalinkWhenTheyArePresent()
    {
        var result = await ServiceFor(Entry(taskName: "Angebot schreiben")).GetEntriesAsync(
            new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27), CancellationToken.None);

        var entry = Assert.Single(result.Entries);
        Assert.Equal("Angebot schreiben", entry.ItemSummary);
        Assert.Equal("https://app.asana.com/0/0/600", entry.ItemUrl);
    }

    /// <summary>Ohne Projekt gibt es kein Abzeichen — der Eintrag bekommt eine sprechende Ersatzkennung.</summary>
    [Fact]
    public async Task EntryWithoutProjectGetsFallbackKeyAndWorkspaceUrl()
    {
        var result = await ServiceFor(Entry(project: null)).GetEntriesAsync(
            new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27), CancellationToken.None);

        var entry = Assert.Single(result.Entries);
        Assert.Equal("Asana", entry.ItemKey);
        Assert.Equal("https://app.asana.com/0/900", entry.ItemUrl);
    }

    [Fact]
    public async Task SkipsEntriesWithoutDuration()
    {
        var result = await ServiceFor(Entry(gid: "1", minutes: 0), Entry(gid: "2", minutes: 30)).GetEntriesAsync(
            new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27), CancellationToken.None);

        Assert.Single(result.Entries);
    }

    [Fact]
    public async Task SortsByDayThenProject()
    {
        var result = await ServiceFor(
            Entry(gid: "1", enteredOn: "2026-09-23", project: "Beta"),
            Entry(gid: "2", enteredOn: "2026-09-21", project: "Zeta"),
            Entry(gid: "3", enteredOn: "2026-09-21", project: "Alpha")).GetEntriesAsync(
            new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27), CancellationToken.None);

        Assert.Equal(new[] { "Alpha", "Zeta", "Beta" }, result.Entries.Select(entry => entry.ItemKey));
    }

    [Fact]
    public async Task EndBeforeStartIsRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => ServiceFor(Entry()).GetEntriesAsync(
            new DateOnly(2026, 9, 27), new DateOnly(2026, 9, 21), CancellationToken.None));
    }
}
