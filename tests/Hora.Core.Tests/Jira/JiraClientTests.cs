using System.Net;
using System.Text.Json;
using Hora.Core.Jira;
using Hora.Core.Tests.Http;

namespace Hora.Core.Tests.Jira;

public class JiraClientTests
{
    /// <summary>
    /// Simuliert einen Verbindungsabriss beim Streamen des Antwortrumpfes, wie es bei
    /// HttpCompletionOption.ResponseHeadersRead tatsächlich passiert: Der Stream wird direkt
    /// durchgereicht (wie bei StreamContent), sodass der Fehler erst beim Lesen auftritt statt
    /// beim Verbindungsaufbau. Ein Umweg über SerializeToStreamAsync würde stattdessen von
    /// HttpContent selbst in eine HttpRequestException verpackt – das würde den hier zu prüfenden
    /// Pfad (rohe IOException beim Lesen) verfehlen.
    /// </summary>
    private sealed class BrokenBodyContent : HttpContent
    {
        protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new ThrowingStream());

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new NotSupportedException("Für diesen Test wird ausschließlich ReadAsStreamAsync verwendet.");

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    /// <summary>Stream, dessen Lesezugriffe eine IOException auslösen, wie bei einem Verbindungsabriss.</summary>
    private sealed class ThrowingStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new IOException("Verbindung beim Lesen des Rumpfes abgerissen.");

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            throw new IOException("Verbindung beim Lesen des Rumpfes abgerissen.");

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new IOException("Verbindung beim Lesen des Rumpfes abgerissen.");

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static (JiraClient client, FakeHttpMessageHandler fake) Create()
    {
        var fake = new FakeHttpMessageHandler();
        var http = new HttpClient(fake) { BaseAddress = new Uri("https://example.atlassian.net/") };
        return (new JiraClient(http), fake);
    }

    [Fact]
    public async Task GetMyselfAsync_reads_account_id_and_display_name()
    {
        var (client, fake) = Create();
        fake.EnqueueJson(HttpStatusCode.OK, """{"accountId":"abc123","displayName":"Simon","timeZone":"Europe/Berlin"}""");

        var user = await client.GetMyselfAsync(CancellationToken.None);

        Assert.Equal("abc123", user.AccountId);
        Assert.Equal("Simon", user.DisplayName);
        Assert.Equal("Europe/Berlin", user.TimeZone);
        Assert.Equal("/rest/api/3/myself", fake.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task SearchIssuesWithWorklogsAsync_follows_next_page_token()
    {
        var (client, fake) = Create();
        fake.EnqueueJson(HttpStatusCode.OK,
            """{"issues":[{"id":"1","key":"ABC-1","fields":{"summary":"Erstes"}},{"id":"2","key":"ABC-2","fields":{"summary":"Zweites"}}],"nextPageToken":"token-2","isLast":false}""");
        fake.EnqueueJson(HttpStatusCode.OK,
            """{"issues":[{"id":"3","key":"ABC-3","fields":{"summary":"Drittes"}}],"isLast":true}""");

        var issues = await client.SearchIssuesWithWorklogsAsync(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20), CancellationToken.None);

        Assert.Equal(new[] { "ABC-1", "ABC-2", "ABC-3" }, issues.Select(issue => issue.Key));
        Assert.Equal("Drittes", issues[2].Fields.Summary);
        Assert.Equal(2, fake.Requests.Count);
        Assert.Equal("/rest/api/3/search/jql", fake.Requests[0].RequestUri!.AbsolutePath);
        Assert.Contains("nextPageToken=token-2", fake.Requests[1].RequestUri!.Query);
    }

    [Fact]
    public async Task SearchIssuesWithWorklogsAsync_returns_an_issue_appearing_on_two_pages_only_once()
    {
        // Befund D: Ohne Entdopplung wurden die Buchungen eines Tickets, das Jira auf zwei Seiten
        // liefert, zweimal abgerufen und damit doppelt gezählt. Erreichbar ab mehr als 100 Tickets
        // im Zeitraum, im Monatsmodus über 42 Tage denkbar.
        var (client, fake) = Create();
        fake.EnqueueJson(HttpStatusCode.OK,
            """{"issues":[{"id":"1","key":"ABC-1","fields":{"summary":"Erstes"}},{"id":"2","key":"ABC-2","fields":{"summary":"Zweites"}}],"nextPageToken":"token-2","isLast":false}""");
        fake.EnqueueJson(HttpStatusCode.OK,
            """{"issues":[{"id":"2","key":"ABC-2","fields":{"summary":"Zweites"}},{"id":"3","key":"ABC-3","fields":{"summary":"Drittes"}}],"isLast":true}""");

        var issues = await client.SearchIssuesWithWorklogsAsync(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20), CancellationToken.None);

        Assert.Equal(new[] { "ABC-1", "ABC-2", "ABC-3" }, issues.Select(issue => issue.Key));
        Assert.Equal(2, fake.Requests.Count);
    }

    [Fact]
    public async Task SearchIssuesWithWorklogsAsync_widens_the_date_range_by_one_day_on_each_side()
    {
        var (client, fake) = Create();
        fake.EnqueueJson(HttpStatusCode.OK, """{"issues":[],"isLast":true}""");

        await client.SearchIssuesWithWorklogsAsync(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20), CancellationToken.None);

        var query = Uri.UnescapeDataString(fake.Requests[0].RequestUri!.Query);
        Assert.Contains("worklogAuthor = currentUser()", query);
        Assert.Contains("worklogDate >= \"2026-09-13\"", query);
        Assert.Contains("worklogDate <= \"2026-09-21\"", query);
        Assert.Contains("fields=summary", query);
        Assert.Contains("maxResults=100", query);
    }

    [Fact]
    public async Task GetWorklogsAsync_pages_with_startAt_until_total_is_reached()
    {
        var (client, fake) = Create();
        fake.EnqueueJson(HttpStatusCode.OK, """
            {"startAt":0,"maxResults":2,"total":3,"worklogs":[
              {"id":"1","issueId":"10","started":"2026-09-16T09:00:00.000+0200","timeSpentSeconds":3600,"author":{"accountId":"me"}},
              {"id":"2","issueId":"10","started":"2026-09-16T11:00:00.000+0200","timeSpentSeconds":1800,"author":{"accountId":"me"}}]}
            """);
        fake.EnqueueJson(HttpStatusCode.OK, """
            {"startAt":2,"maxResults":2,"total":3,"worklogs":[
              {"id":"3","issueId":"10","started":"2026-09-16T14:00:00.000+0200","timeSpentSeconds":900,"author":{"accountId":"me"}}]}
            """);
        var after = new DateTimeOffset(2026, 9, 13, 22, 0, 0, TimeSpan.Zero);
        var before = new DateTimeOffset(2026, 9, 20, 22, 0, 0, TimeSpan.Zero);

        var worklogs = await client.GetWorklogsAsync("ABC-1", after, before, CancellationToken.None);

        Assert.Equal(new[] { "1", "2", "3" }, worklogs.Select(worklog => worklog.Id));
        Assert.Equal(2, fake.Requests.Count);
        Assert.Equal("/rest/api/3/issue/ABC-1/worklog", fake.Requests[0].RequestUri!.AbsolutePath);
        Assert.Contains($"startedAfter={after.ToUnixTimeMilliseconds()}", fake.Requests[0].RequestUri!.Query);
        Assert.Contains($"startedBefore={before.ToUnixTimeMilliseconds()}", fake.Requests[0].RequestUri!.Query);
        Assert.Contains("startAt=0", fake.Requests[0].RequestUri!.Query);
        Assert.Contains("startAt=2", fake.Requests[1].RequestUri!.Query);
    }

    [Fact]
    public async Task GetWorklogsAsync_returns_empty_list_when_issue_has_no_worklogs()
    {
        var (client, fake) = Create();
        fake.EnqueueJson(HttpStatusCode.OK, """{"startAt":0,"maxResults":100,"total":0,"worklogs":[]}""");

        var worklogs = await client.GetWorklogsAsync("ABC-1", DateTimeOffset.UnixEpoch, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Empty(worklogs);
        Assert.Single(fake.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Authentication_errors_throw_JiraAuthenticationException(HttpStatusCode status)
    {
        var (client, fake) = Create();
        fake.EnqueueJson(status, """{"message":"nope"}""");

        await Assert.ThrowsAsync<JiraAuthenticationException>(() => client.GetMyselfAsync(CancellationToken.None));
    }

    [Fact]
    public async Task TooManyRequests_throws_JiraRateLimitException()
    {
        var (client, fake) = Create();
        fake.EnqueueJson(HttpStatusCode.TooManyRequests, "{}");

        await Assert.ThrowsAsync<JiraRateLimitException>(() => client.GetMyselfAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Server_error_throws_JiraUnavailableException_with_status_code()
    {
        var (client, fake) = Create();
        fake.EnqueueJson(HttpStatusCode.InternalServerError, "{}");

        var exception = await Assert.ThrowsAsync<JiraUnavailableException>(() => client.GetMyselfAsync(CancellationToken.None));

        Assert.Equal(500, exception.StatusCode);
    }

    [Fact]
    public async Task Network_failure_throws_JiraUnavailableException_without_status_code()
    {
        var (client, fake) = Create();
        fake.EnqueueThrow(new HttpRequestException("offline"));

        var exception = await Assert.ThrowsAsync<JiraUnavailableException>(() => client.GetMyselfAsync(CancellationToken.None));

        Assert.Null(exception.StatusCode);
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task Cancellation_is_propagated_as_OperationCanceledException()
    {
        var (client, fake) = Create();
        fake.EnqueueJson(HttpStatusCode.OK, "{}");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetMyselfAsync(cts.Token));
    }

    [Fact]
    public async Task Timeout_throws_JiraUnavailableException_without_status_code()
    {
        var (client, fake) = Create();
        fake.EnqueueThrow(new TaskCanceledException("timeout"));

        var exception = await Assert.ThrowsAsync<JiraUnavailableException>(() => client.GetMyselfAsync(CancellationToken.None));

        Assert.Null(exception.StatusCode);
        Assert.IsType<TaskCanceledException>(exception.InnerException);
    }

    [Fact]
    public async Task Malformed_json_throws_JiraUnavailableException_instead_of_JsonException()
    {
        var (client, fake) = Create();
        fake.EnqueueJson(HttpStatusCode.OK, "{not valid json");

        var exception = await Assert.ThrowsAsync<JiraUnavailableException>(() => client.GetMyselfAsync(CancellationToken.None));

        Assert.IsType<JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task Body_read_failure_throws_JiraUnavailableException()
    {
        var (client, fake) = Create();
        fake.EnqueueJson(HttpStatusCode.OK, "{}", response => response.Content = new BrokenBodyContent());

        var exception = await Assert.ThrowsAsync<JiraUnavailableException>(() => client.GetMyselfAsync(CancellationToken.None));

        Assert.IsType<IOException>(exception.InnerException);
    }

    [Fact]
    public async Task SearchIssuesWithWorklogsAsync_stops_instead_of_looping_forever_on_a_repeated_page_token()
    {
        var (client, fake) = Create();
        fake.EnqueueJson(HttpStatusCode.OK,
            """{"issues":[{"id":"1","key":"ABC-1","fields":{"summary":"Erstes"}}],"nextPageToken":"stuck-token","isLast":false}""");
        fake.EnqueueJson(HttpStatusCode.OK,
            """{"issues":[{"id":"2","key":"ABC-2","fields":{"summary":"Zweites"}}],"nextPageToken":"stuck-token","isLast":false}""");

        var issues = await client.SearchIssuesWithWorklogsAsync(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20), CancellationToken.None);

        Assert.Equal(new[] { "ABC-1", "ABC-2" }, issues.Select(issue => issue.Key));
        Assert.Equal(2, fake.Requests.Count);
    }
}
