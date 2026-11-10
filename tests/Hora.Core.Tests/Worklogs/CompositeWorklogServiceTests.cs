using Hora.Core.Everhour;
using Hora.Core.Logging;
using Hora.Core.Worklogs;

namespace Hora.Core.Tests.Worklogs;

public class CompositeWorklogServiceTests
{
    private sealed class NullLogger : IAppLogger
    {
        public void Info(string message)
        {
        }

        public void Error(string message, Exception? exception = null)
        {
        }
    }

    /// <summary>Liefert eine feste Menge oder wirft — je nachdem, womit er gebaut wurde.</summary>
    private sealed class StubWorklogService : IWorklogService
    {
        private readonly WorklogFetchResult _result;
        private readonly Exception? _throw;

        public StubWorklogService(WorklogFetchResult result)
        {
            _result = result;
        }

        public StubWorklogService(Exception toThrow)
        {
            _throw = toThrow;
            _result = Empty;
        }

        public Task<WorklogFetchResult> GetEntriesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
            _throw is not null ? throw _throw : Task.FromResult(_result);
    }

    private static readonly WorklogFetchResult Empty =
        new(Array.Empty<WorklogEntry>(), Array.Empty<string>(), Array.Empty<string>());

    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    private static readonly DateOnly From = new(2026, 9, 21);
    private static readonly DateOnly To = new(2026, 9, 27);

    private static WorklogEntry JiraEntry(string key, int hour) => WorklogEntry.Jira(
        key, key, new DateTimeOffset(2026, 9, 21, hour, 0, 0, TimeSpan.FromHours(2)),
        TimeSpan.FromHours(1), string.Empty, "https://example.atlassian.net");

    private static WorklogEntry AsanaEntry(string key, string? taskName = null) => new(
        WorklogSource.Asana, key, taskName ?? key,
        new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2)),
        TimeSpan.FromHours(2), string.Empty, "https://app.asana.com/0/1", HasStartTime: false);

    private static StubWorklogService With(params WorklogEntry[] entries) =>
        new(new WorklogFetchResult(entries, Array.Empty<string>(), Array.Empty<string>()));

    private static CompositeWorklogService Composite(IWorklogService jira, IWorklogService? asana = null)
    {
        var composite = new CompositeWorklogService(jira, new NullLogger(), Berlin);
        composite.SetAsanaProvider(asana);
        return composite;
    }

    [Fact]
    public async Task WithoutAsanaItReturnsExactlyTheJiraResult()
    {
        var result = await Composite(With(JiraEntry("A-1", 9))).GetEntriesAsync(From, To, CancellationToken.None);

        Assert.Equal("A-1", Assert.Single(result.Entries).ItemKey);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task MergesBothSourcesSortedByStartThenSourceThenKey()
    {
        var composite = Composite(
            With(JiraEntry("A-1", 9), JiraEntry("A-2", 14)),
            With(AsanaEntry("Marketing"), AsanaEntry("Alpha")));

        var result = await composite.GetEntriesAsync(From, To, CancellationToken.None);

        // Asana liegt auf Mitternacht und steht deshalb vorn; innerhalb desselben Zeitstempels
        // sortiert der Schlüssel, damit die Reihenfolge über Abrufe hinweg stabil bleibt.
        Assert.Equal(new[] { "Alpha", "Marketing", "A-1", "A-2" }, result.Entries.Select(entry => entry.ItemKey));
    }

    [Fact]
    public async Task KeepsJiraFailedIssueKeys()
    {
        var jira = new StubWorklogService(new WorklogFetchResult(
            new[] { JiraEntry("A-1", 9) }, new[] { "B-2" }, Array.Empty<string>()));

        var result = await Composite(jira, With(AsanaEntry("Marketing"))).GetEntriesAsync(From, To, CancellationToken.None);

        Assert.Equal("B-2", Assert.Single(result.FailedIssueKeys));
    }

    /// <summary>Der tragende Grundsatz: Ein Everhour-Fehler darf die Jira-Zahlen nicht antasten.</summary>
    [Theory]
    [InlineData(typeof(EverhourUnavailableException))]
    [InlineData(typeof(EverhourRateLimitException))]
    [InlineData(typeof(InvalidOperationException))]
    public async Task EverhourFailureLeavesJiraEntriesStandingAndAddsAWarning(Type exceptionType)
    {
        var exception = exceptionType == typeof(EverhourUnavailableException)
            ? new EverhourUnavailableException("weg")
            : (Exception)Activator.CreateInstance(exceptionType)!;
        var composite = Composite(With(JiraEntry("A-1", 9)), new StubWorklogService(exception));

        var result = await composite.GetEntriesAsync(From, To, CancellationToken.None);

        Assert.Equal("A-1", Assert.Single(result.Entries).ItemKey);
        Assert.Contains("Asana", Assert.Single(result.Warnings));
    }

    [Fact]
    public async Task RejectedKeyRaisesTheEventAndWarns()
    {
        var composite = Composite(With(JiraEntry("A-1", 9)), new StubWorklogService(new EverhourAuthenticationException()));
        var raised = 0;
        composite.AsanaTokenRejected += () => raised++;

        var result = await composite.GetEntriesAsync(From, To, CancellationToken.None);

        Assert.Equal(1, raised);
        Assert.Single(result.Entries);
        Assert.Contains("neu verbinden", Assert.Single(result.Warnings));
    }

    /// <summary>
    /// Der zweite Grundsatz, als Gesamtprobe: Jira 1 h auf SYN-2409, Everhour 2 h auf dem
    /// Asana-Spiegel desselben Tickets am selben Tag, dazu 2 h Meetings. Für das Ticket zählt 1 h,
    /// in der Summe also 3 h — nie 5 h.
    /// </summary>
    [Fact]
    public async Task AsanaMirrorOfATicketBookedInJiraThatDayIsNotCountedTwice()
    {
        var composite = Composite(
            With(JiraEntry("SYN-2409", 10)),
            With(AsanaEntry("Projekt", "SYN-2409 – Varda - Oberfläche"), AsanaEntry("Meetings", "1:1 Meetings")));

        var result = await composite.GetEntriesAsync(From, To, CancellationToken.None);

        Assert.Equal(new[] { "1:1 Meetings", "SYN-2409" }, result.Entries.Select(entry => entry.ItemSummary));
        Assert.Equal(TimeSpan.FromHours(3), TimeSpan.FromTicks(result.Entries.Sum(entry => entry.Duration.Ticks)));
        Assert.Contains("SYN-2409 (21.09.)", Assert.Single(result.Warnings));
    }

    [Fact]
    public async Task AsanaMirrorOfATicketJiraCouldNotLoadIsHidden()
    {
        var jira = new StubWorklogService(new WorklogFetchResult(
            Array.Empty<WorklogEntry>(), new[] { "SYN-2409" }, Array.Empty<string>()));

        var result = await Composite(jira, With(AsanaEntry("Projekt", "SYN-2409 – Varda"))).GetEntriesAsync(From, To, CancellationToken.None);

        Assert.Empty(result.Entries);
        Assert.Contains("SYN-2409", Assert.Single(result.Warnings));
    }

    /// <summary>Ein Jira-Fehler bleibt ein Jira-Fehler: Die Hülle unterscheidet daran Sitzungsende und Störung.</summary>
    [Fact]
    public async Task JiraFailurePropagates()
    {
        var composite = Composite(
            new StubWorklogService(new Hora.Core.Jira.JiraAuthenticationException()),
            With(AsanaEntry("Marketing")));

        await Assert.ThrowsAsync<Hora.Core.Jira.JiraAuthenticationException>(
            () => composite.GetEntriesAsync(From, To, CancellationToken.None));
    }

    [Fact]
    public async Task CancellationIsNotSwallowedAsAnEverhourWarning()
    {
        var composite = Composite(With(JiraEntry("A-1", 9)), new StubWorklogService(new OperationCanceledException()));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => composite.GetEntriesAsync(From, To, CancellationToken.None));
    }

    [Fact]
    public async Task ProviderCanBeRemovedAgain()
    {
        var composite = Composite(With(JiraEntry("A-1", 9)), With(AsanaEntry("Marketing")));
        Assert.Equal(2, (await composite.GetEntriesAsync(From, To, CancellationToken.None)).Entries.Count);

        composite.SetAsanaProvider(null);

        Assert.Single((await composite.GetEntriesAsync(From, To, CancellationToken.None)).Entries);
    }
}
