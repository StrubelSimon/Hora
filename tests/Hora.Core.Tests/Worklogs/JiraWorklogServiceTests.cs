using System.Text.Json;
using Hora.Core.Jira;
using Hora.Core.Jira.Models;
using Hora.Core.Logging;
using Hora.Core.Worklogs;

namespace Hora.Core.Tests.Worklogs;

public class JiraWorklogServiceTests
{
    private const string Me = "me-account";
    private const string BaseUrl = "https://example.atlassian.net";
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    private static readonly DateOnly Monday = new(2026, 9, 14);
    private static readonly DateOnly Sunday = new(2026, 9, 20);

    private sealed class FakeJiraClient : IJiraClient
    {
        private int _running;

        public List<JiraIssue> Issues { get; } = new();
        public Dictionary<string, List<JiraWorklog>> WorklogsByIssue { get; } = new();
        public Dictionary<string, Exception> FailuresByIssue { get; } = new();
        public List<(string JiraIssueKey, DateTimeOffset After, DateTimeOffset Before)> WorklogRequests { get; } = new();
        public int MaxObservedParallelism { get; private set; }

        public Task<JiraUser> GetMyselfAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new JiraUser { AccountId = Me, DisplayName = "Ich" });

        public Task<IReadOnlyList<JiraIssue>> SearchIssuesWithWorklogsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<JiraIssue>>(Issues);

        public async Task<IReadOnlyList<JiraWorklog>> GetWorklogsAsync(string issueKey, DateTimeOffset startedAfter, DateTimeOffset startedBefore, CancellationToken cancellationToken)
        {
            var running = Interlocked.Increment(ref _running);
            lock (WorklogRequests)
            {
                WorklogRequests.Add((issueKey, startedAfter, startedBefore));
                MaxObservedParallelism = Math.Max(MaxObservedParallelism, running);
            }

            try
            {
                await Task.Delay(20, cancellationToken);
                if (FailuresByIssue.TryGetValue(issueKey, out var failure))
                {
                    throw failure;
                }

                return WorklogsByIssue.TryGetValue(issueKey, out var worklogs) ? worklogs : new List<JiraWorklog>();
            }
            finally
            {
                Interlocked.Decrement(ref _running);
            }
        }
    }

    private sealed class RecordingLogger : IAppLogger
    {
        public List<string> Errors { get; } = new();
        public void Info(string message) { }
        public void Error(string message, Exception? exception = null) => Errors.Add(message);
    }

    private static JiraIssue Issue(string key, string summary = "Titel") =>
        new() { Id = key, Key = key, Fields = new JiraIssueFields { Summary = summary } };

    private static JiraWorklog Worklog(string accountId, string started, long seconds, string? comment = null) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Author = new JiraUser { AccountId = accountId },
        Started = started,
        TimeSpentSeconds = seconds,
        Comment = comment is null
            ? null
            : JsonSerializer.SerializeToElement(new
            {
                type = "doc",
                version = 1,
                content = new[] { new { type = "paragraph", content = new[] { new { type = "text", text = comment } } } },
            }),
    };

    private static (JiraWorklogService service, FakeJiraClient jira, RecordingLogger logger) Create()
    {
        var jira = new FakeJiraClient();
        var logger = new RecordingLogger();
        return (new JiraWorklogService(jira, Me, BaseUrl, Berlin, logger), jira, logger);
    }

    [Fact]
    public async Task Keeps_only_own_worklogs()
    {
        var (service, jira, _) = Create();
        jira.Issues.Add(Issue("ABC-1"));
        jira.WorklogsByIssue["ABC-1"] = new()
        {
            Worklog(Me, "2026-09-16T09:00:00.000+0200", 3600),
            Worklog("someone-else", "2026-09-16T10:00:00.000+0200", 1800),
        };

        var result = await service.GetEntriesAsync(Monday, Sunday, CancellationToken.None);

        var entry = Assert.Single(result.Entries);
        Assert.Equal(TimeSpan.FromHours(1), entry.Duration);
    }

    [Fact]
    public async Task Keeps_only_worklogs_inside_the_local_date_range()
    {
        var (service, jira, _) = Create();
        jira.Issues.Add(Issue("ABC-1"));
        jira.WorklogsByIssue["ABC-1"] = new()
        {
            Worklog(Me, "2026-09-13T23:30:00.000+0200", 60),  // Sonntag davor
            Worklog(Me, "2026-09-14T00:00:00.000+0200", 120), // erste Minute der Woche
            Worklog(Me, "2026-09-20T23:59:00.000+0200", 180), // letzte Minute der Woche
            Worklog(Me, "2026-09-21T00:00:00.000+0200", 240), // Montag danach
        };

        var result = await service.GetEntriesAsync(Monday, Sunday, CancellationToken.None);

        Assert.Equal(new[] { 120L, 180L }, result.Entries.Select(entry => (long)entry.Duration.TotalSeconds));
    }

    [Fact]
    public async Task Passes_local_midnight_bounds_to_the_client()
    {
        var (service, jira, _) = Create();
        jira.Issues.Add(Issue("ABC-1"));

        await service.GetEntriesAsync(Monday, Sunday, CancellationToken.None);

        var request = Assert.Single(jira.WorklogRequests);
        // Berlin ist im September UTC+2: lokale Mitternacht am 14.9. entspricht 13.9. 22:00 UTC.
        Assert.Equal(new DateTimeOffset(2026, 9, 13, 22, 0, 0, TimeSpan.Zero).AddMilliseconds(-1), request.After);
        Assert.Equal(new DateTimeOffset(2026, 9, 20, 22, 0, 0, TimeSpan.Zero), request.Before);
    }

    [Fact]
    public async Task Maps_all_entry_fields_including_source_and_address()
    {
        var (service, jira, _) = Create();
        jira.Issues.Add(Issue("ABC-7", "Login-Maske bauen"));
        jira.WorklogsByIssue["ABC-7"] = new() { Worklog(Me, "2026-09-16T09:00:00.000+0200", 5400, "Review mit Team") };

        var result = await service.GetEntriesAsync(Monday, Sunday, CancellationToken.None);

        var entry = Assert.Single(result.Entries);
        Assert.Equal("ABC-7", entry.ItemKey);
        Assert.Equal("Login-Maske bauen", entry.ItemSummary);
        Assert.Equal(TimeSpan.FromMinutes(90), entry.Duration);
        Assert.Equal("Review mit Team", entry.Comment);
        Assert.Equal(new DateTimeOffset(2026, 9, 16, 9, 0, 0, TimeSpan.FromHours(2)), entry.Started);

        // Der ganze Weg von baseUrl bis zur fertigen Ticketadresse, als ausgeschriebener Wert statt
        // als nachgerechneter: Reicht der Dienst der Fabrik versehentlich ein anderes Feld durch,
        // fiele das sonst nirgends auf — die Adresse baut seit Task 6 niemand sonst mehr zusammen.
        Assert.Equal("https://example.atlassian.net/browse/ABC-7", entry.ItemUrl);
        Assert.Equal(WorklogSource.Jira, entry.Source);
        // Jira-Worklogs tragen immer eine Uhrzeit; allein daran entscheidet die Zeile ihren Zeitblock.
        Assert.True(entry.HasStartTime);
    }

    [Fact]
    public async Task Collects_failed_issues_and_keeps_the_rest()
    {
        var (service, jira, logger) = Create();
        jira.Issues.Add(Issue("ABC-1"));
        jira.Issues.Add(Issue("ABC-2"));
        jira.WorklogsByIssue["ABC-1"] = new() { Worklog(Me, "2026-09-16T09:00:00.000+0200", 600) };
        jira.FailuresByIssue["ABC-2"] = new JiraUnavailableException("HTTP 500", 500);

        var result = await service.GetEntriesAsync(Monday, Sunday, CancellationToken.None);

        Assert.Single(result.Entries);
        Assert.Equal(new[] { "ABC-2" }, result.FailedIssueKeys);
        Assert.Single(logger.Errors);
    }

    [Fact]
    public async Task Issue_without_fields_is_reported_as_a_partial_failure_instead_of_ending_the_fetch()
    {
        // Befund E: Liefert Jira "fields": null, wirft der Zugriff auf den Titel eine
        // NullReferenceException. Sie fiel bis dahin aus Task.WhenAll heraus und beendete den
        // gesamten Abruf, statt dieses eine Ticket als Teilfehler zu melden.
        var (service, jira, logger) = Create();
        jira.Issues.Add(Issue("ABC-1"));
        jira.Issues.Add(new JiraIssue { Id = "ABC-2", Key = "ABC-2", Fields = null! });
        jira.WorklogsByIssue["ABC-1"] = new() { Worklog(Me, "2026-09-16T09:00:00.000+0200", 600) };
        jira.WorklogsByIssue["ABC-2"] = new() { Worklog(Me, "2026-09-16T11:00:00.000+0200", 600) };

        var result = await service.GetEntriesAsync(Monday, Sunday, CancellationToken.None);

        var entry = Assert.Single(result.Entries);
        Assert.Equal("ABC-1", entry.ItemKey);
        Assert.Equal(new[] { "ABC-2" }, result.FailedIssueKeys);
        Assert.Single(logger.Errors);
    }

    [Fact]
    public async Task Authentication_failure_aborts_the_whole_fetch()
    {
        var (service, jira, _) = Create();
        jira.Issues.Add(Issue("ABC-1"));
        jira.FailuresByIssue["ABC-1"] = new JiraAuthenticationException();

        await Assert.ThrowsAsync<JiraAuthenticationException>(() => service.GetEntriesAsync(Monday, Sunday, CancellationToken.None));
    }

    [Fact]
    public async Task Limits_parallel_worklog_requests_to_four()
    {
        var (service, jira, _) = Create();
        for (var i = 1; i <= 12; i++)
        {
            jira.Issues.Add(Issue($"ABC-{i}"));
        }

        await service.GetEntriesAsync(Monday, Sunday, CancellationToken.None);

        Assert.Equal(12, jira.WorklogRequests.Count);
        Assert.InRange(jira.MaxObservedParallelism, 1, 4);
    }

    [Fact]
    public async Task Sorts_entries_by_start_time_across_issues()
    {
        var (service, jira, _) = Create();
        jira.Issues.Add(Issue("ABC-1"));
        jira.Issues.Add(Issue("ABC-2"));
        jira.WorklogsByIssue["ABC-1"] = new() { Worklog(Me, "2026-09-16T14:00:00.000+0200", 60) };
        jira.WorklogsByIssue["ABC-2"] = new() { Worklog(Me, "2026-09-15T09:00:00.000+0200", 60) };

        var result = await service.GetEntriesAsync(Monday, Sunday, CancellationToken.None);

        Assert.Equal(new[] { "ABC-2", "ABC-1" }, result.Entries.Select(entry => entry.ItemKey));
    }

    [Fact]
    public async Task Sorts_entries_with_the_same_start_time_by_issue_key()
    {
        // Befund G: Die Sortierung nach Startzeit ist stabil, die Aufzählungsreihenfolge der
        // nebenläufig gefüllten ConcurrentBag davor aber nicht. Zwei Buchungen mit identischem
        // Zeitstempel tauschten deshalb bei jedem Aktualisieren die Plätze. Der Abruf läuft hier
        // mehrfach, weil ein einzelner Durchlauf die zufällige Reihenfolge nicht zuverlässig zeigt.
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var (service, jira, _) = Create();
            // Bewusst in umgekehrter Reihenfolge angemeldet: Die Aufgaben starten so mit ZZZ-9.
            jira.Issues.Add(Issue("ZZZ-9"));
            jira.Issues.Add(Issue("AAA-1"));
            jira.WorklogsByIssue["ZZZ-9"] = new() { Worklog(Me, "2026-09-16T09:00:00.000+0200", 600) };
            jira.WorklogsByIssue["AAA-1"] = new() { Worklog(Me, "2026-09-16T09:00:00.000+0200", 600) };

            var result = await service.GetEntriesAsync(Monday, Sunday, CancellationToken.None);

            Assert.Equal(new[] { "AAA-1", "ZZZ-9" }, result.Entries.Select(entry => entry.ItemKey));
        }
    }

    [Fact]
    public async Task Rejects_range_where_to_is_before_from()
    {
        var (service, _, _) = Create();

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetEntriesAsync(Sunday, Monday, CancellationToken.None));
    }
}
