using Hora.Core.Asana;
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

    private static readonly DateOnly From = new(2026, 9, 21);
    private static readonly DateOnly To = new(2026, 9, 27);

    private static WorklogEntry JiraEntry(string key, int hour) => WorklogEntry.Jira(
        key, key, new DateTimeOffset(2026, 9, 21, hour, 0, 0, TimeSpan.FromHours(2)),
        TimeSpan.FromHours(1), string.Empty, "https://example.atlassian.net");

    private static WorklogEntry AsanaEntry(string key) => new(
        WorklogSource.Asana, key, key,
        new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2)),
        TimeSpan.FromHours(2), string.Empty, "https://app.asana.com/0/1", HasStartTime: false);

    private static StubWorklogService With(params WorklogEntry[] entries) =>
        new(new WorklogFetchResult(entries, Array.Empty<string>(), Array.Empty<string>()));

    private static CompositeWorklogService Composite(IWorklogService jira, IWorklogService? asana = null)
    {
        var composite = new CompositeWorklogService(jira, new NullLogger());
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

    /// <summary>Der tragende Grundsatz: Ein Asana-Fehler darf die Jira-Zahlen nicht antasten.</summary>
    [Theory]
    [InlineData(typeof(AsanaUnavailableException))]
    [InlineData(typeof(AsanaFeatureUnavailableException))]
    [InlineData(typeof(AsanaRateLimitException))]
    public async Task AsanaFailureLeavesJiraEntriesStandingAndAddsAWarning(Type exceptionType)
    {
        var exception = exceptionType == typeof(AsanaUnavailableException)
            ? new AsanaUnavailableException("weg")
            : (Exception)Activator.CreateInstance(exceptionType)!;
        var composite = Composite(With(JiraEntry("A-1", 9)), new StubWorklogService(exception));

        var result = await composite.GetEntriesAsync(From, To, CancellationToken.None);

        Assert.Equal("A-1", Assert.Single(result.Entries).ItemKey);
        Assert.Single(result.Warnings);
        Assert.Contains("Asana", Assert.Single(result.Warnings));
    }

    [Fact]
    public async Task MissingTierIsNamedInTheWarning()
    {
        var composite = Composite(With(JiraEntry("A-1", 9)), new StubWorklogService(new AsanaFeatureUnavailableException()));

        var result = await composite.GetEntriesAsync(From, To, CancellationToken.None);

        Assert.Contains("Tarif", Assert.Single(result.Warnings));
    }

    [Fact]
    public async Task RevokedTokenRaisesTheEventAndWarns()
    {
        var composite = Composite(With(JiraEntry("A-1", 9)), new StubWorklogService(new AsanaAuthenticationException()));
        var raised = 0;
        composite.AsanaTokenRejected += () => raised++;

        var result = await composite.GetEntriesAsync(From, To, CancellationToken.None);

        Assert.Equal(1, raised);
        Assert.Single(result.Entries);
        Assert.Contains("neu verbinden", Assert.Single(result.Warnings));
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
    public async Task CancellationIsNotSwallowedAsAnAsanaWarning()
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
