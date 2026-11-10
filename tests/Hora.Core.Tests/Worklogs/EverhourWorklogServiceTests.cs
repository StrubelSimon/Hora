using Hora.Core.Everhour;
using Hora.Core.Everhour.Models;
using Hora.Core.Tests.Everhour;
using Hora.Core.Worklogs;

namespace Hora.Core.Tests.Worklogs;

public class EverhourWorklogServiceTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    private static readonly DateOnly From = new(2026, 11, 2);
    private static readonly DateOnly To = new(2026, 11, 8);

    private const string MeetingsProject = "as:1212635970393658";

    private static EverhourTimeEntry Entry(
        long id = 1,
        string date = "2026-11-03",
        int seconds = 4800,
        string? taskId = "as:1220389840924623",
        string taskName = "Synthara - Strategie",
        string? comment = null,
        params string[] projects)
    {
        return new EverhourTimeEntry
        {
            Id = id,
            Date = DateOnly.Parse(date),
            Time = seconds,
            Comment = comment,
            Task = taskId is null
                ? null
                : new EverhourTask
                {
                    Id = taskId,
                    Name = taskName,
                    Url = $"https://app.asana.com/0/0/{id}/f",
                    Projects = projects.Length > 0 ? projects.ToList() : new List<string> { MeetingsProject },
                },
        };
    }

    private static (EverhourWorklogService Service, FakeEverhourClient Client) ServiceFor(params EverhourTimeEntry[] entries)
    {
        var client = new FakeEverhourClient(entries);
        client.ProjectNames[MeetingsProject] = "2026-I-02|DT|-interne-Meetings";
        return (new EverhourWorklogService(client, 1431001, Berlin), client);
    }

    [Fact]
    public async Task MapsAnAsanaEntryOntoTheAsanaSource()
    {
        var (service, client) = ServiceFor(Entry(comment: "Abstimmung"));

        var entry = Assert.Single((await service.GetEntriesAsync(From, To, CancellationToken.None)).Entries);

        Assert.Equal(WorklogSource.Asana, entry.Source);
        Assert.Equal("2026-I-02|DT|-interne-Meetings", entry.ItemKey);
        Assert.Equal("Synthara - Strategie", entry.ItemSummary);
        Assert.Equal(TimeSpan.FromSeconds(4800), entry.Duration);
        Assert.Equal("Abstimmung", entry.Comment);
        Assert.Equal("https://app.asana.com/0/0/1/f", entry.ItemUrl);
        Assert.False(entry.HasStartTime);
        Assert.Equal(1431001, client.LastUserId);
        Assert.Equal(From, client.LastFrom);
        Assert.Equal(To, client.LastTo);
    }

    /// <summary>Lokale Mitternacht des gebuchten Tags — so landet der Eintrag auf genau diesem Kalendertag.</summary>
    [Fact]
    public async Task StartsAtLocalMidnightOfTheBookedDay()
    {
        var (service, _) = ServiceFor(Entry(date: "2026-11-03"));

        var entry = Assert.Single((await service.GetEntriesAsync(From, To, CancellationToken.None)).Entries);

        Assert.Equal(new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.FromHours(1)), entry.Started);
        Assert.Equal(new DateOnly(2026, 11, 3), entry.LocalDate(Berlin));
    }

    /// <summary>Regel 1: Für Jira-Zeiten ist allein Jira die Quelle. Everhours Jira-Kopien zählen nie.</summary>
    [Fact]
    public async Task DropsEverhoursOwnJiraTasks()
    {
        var (service, _) = ServiceFor(Entry(taskId: "jr:8403-41500", taskName: "Varda - Oberfläche"));

        Assert.Empty((await service.GetEntriesAsync(From, To, CancellationToken.None)).Entries);
    }

    [Fact]
    public async Task DropsEntriesWithoutTaskAndWithoutDuration()
    {
        var (service, _) = ServiceFor(
            Entry(id: 1, taskId: null),
            Entry(id: 2, seconds: 0));

        Assert.Empty((await service.GetEntriesAsync(From, To, CancellationToken.None)).Entries);
    }

    /// <summary>
    /// Regel 2: Ein Kommentar der Form „[SYN-2204#wl-42234]“ kennzeichnet die Kopie eines
    /// Jira-Worklogs. Sie fällt immer weg — auch ohne passenden Jira-Eintrag im selben Abruf.
    /// </summary>
    [Theory]
    [InlineData("[SYN-2204#wl-42234]")]
    [InlineData("Review [SYN-2057#wl-41997] Nacharbeit")]
    public async Task DropsCopiesOfJiraWorklogs(string comment)
    {
        var (service, _) = ServiceFor(Entry(taskName: "SYN-2204 – Chatbot - Agent Sammelticket", comment: comment));

        Assert.Empty((await service.GetEntriesAsync(From, To, CancellationToken.None)).Entries);
    }

    [Fact]
    public async Task KeepsCommentsThatOnlyLookSimilar()
    {
        var (service, _) = ServiceFor(Entry(comment: "Workshop #wl-Vorbereitung"));

        Assert.Single((await service.GetEntriesAsync(From, To, CancellationToken.None)).Entries);
    }

    [Fact]
    public async Task AsksForEachProjectNameOnlyOnce()
    {
        var (service, client) = ServiceFor(Entry(id: 1), Entry(id: 2, date: "2026-11-04"));

        await service.GetEntriesAsync(From, To, CancellationToken.None);
        await service.GetEntriesAsync(From, To, CancellationToken.None);

        Assert.Equal(MeetingsProject, Assert.Single(client.RequestedProjects));
    }

    /// <summary>Ein fehlender Projektname kostet nur das Abzeichen, nie die Zeit.</summary>
    [Fact]
    public async Task FailedProjectLookupFallsBackToAsana()
    {
        var (service, _) = ServiceFor(Entry(projects: "as:999"));

        var entry = Assert.Single((await service.GetEntriesAsync(From, To, CancellationToken.None)).Entries);

        Assert.Equal("Asana", entry.ItemKey);
        Assert.Equal(TimeSpan.FromSeconds(4800), entry.Duration);
    }

    [Fact]
    public async Task RejectedKeyDuringProjectLookupIsNotSwallowed()
    {
        var (service, client) = ServiceFor(Entry(projects: "as:999"));
        client.ProjectFailure = new EverhourAuthenticationException();

        await Assert.ThrowsAsync<EverhourAuthenticationException>(
            () => service.GetEntriesAsync(From, To, CancellationToken.None));
    }

    [Fact]
    public async Task SortsByDayThenTaskName()
    {
        var (service, _) = ServiceFor(
            Entry(id: 1, date: "2026-11-04", taskName: "Alpha"),
            Entry(id: 2, date: "2026-11-03", taskName: "Zulu"),
            Entry(id: 3, date: "2026-11-03", taskName: "Bravo"));

        var result = await service.GetEntriesAsync(From, To, CancellationToken.None);

        Assert.Equal(new[] { "Bravo", "Zulu", "Alpha" }, result.Entries.Select(entry => entry.ItemSummary));
    }

    [Fact]
    public async Task EndBeforeStartIsRejected()
    {
        var (service, _) = ServiceFor();

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetEntriesAsync(To, From, CancellationToken.None));
    }
}
