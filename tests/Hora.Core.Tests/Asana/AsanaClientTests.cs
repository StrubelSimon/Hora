using System.Net;
using Hora.Core.Asana;
using Hora.Core.Tests.Http;

namespace Hora.Core.Tests.Asana;

public class AsanaClientTests
{
    private static AsanaClient ClientFor(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri(AsanaClient.BaseUrl) });

    [Fact]
    public async Task GetMyselfRequestsWorkspacesAndReadsThem()
    {
        var handler = new FakeHttpMessageHandler().EnqueueJson(HttpStatusCode.OK, """
        {"data":{"gid":"111","name":"Simon Strubel","email":"simon.strubel@advisori.de",
        "workspaces":[{"gid":"900","name":"ADVISORI"}]}}
        """);

        var user = await ClientFor(handler).GetMyselfAsync(CancellationToken.None);

        Assert.Equal("111", user.Gid);
        Assert.Equal("ADVISORI", Assert.Single(user.Workspaces).Name);
        var url = Assert.Single(handler.Requests).RequestUri!.ToString();
        Assert.Contains("users/me", url);
        Assert.Contains("opt_fields=", url);
    }

    [Fact]
    public async Task TimeEntriesSendUserWorkspaceAndBothInclusiveDates()
    {
        var handler = new FakeHttpMessageHandler().EnqueueJson(HttpStatusCode.OK, """{"data":[]}""");

        await ClientFor(handler).GetTimeEntriesAsync(
            "111", "900", new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27), CancellationToken.None);

        var url = Assert.Single(handler.Requests).RequestUri!.ToString();
        Assert.Contains("time_tracking_entries", url);
        Assert.Contains("user=111", url);
        Assert.Contains("workspace=900", url);
        Assert.Contains("entered_on_start_date=2026-09-21", url);
        Assert.Contains("entered_on_end_date=2026-09-27", url);
        Assert.Contains("limit=100", url);
    }

    [Fact]
    public async Task FollowsNextPageOffsetUntilItIsNull()
    {
        var handler = new FakeHttpMessageHandler()
            .EnqueueJson(HttpStatusCode.OK, """
            {"data":[{"gid":"1","duration_minutes":60,"entered_on":"2026-09-21"}],
             "next_page":{"offset":"page2"}}
            """)
            .EnqueueJson(HttpStatusCode.OK, """
            {"data":[{"gid":"2","duration_minutes":30,"entered_on":"2026-09-22"}],"next_page":null}
            """);

        var entries = await ClientFor(handler).GetTimeEntriesAsync(
            "111", "900", new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27), CancellationToken.None);

        Assert.Equal(2, entries.Count);
        Assert.Equal(new[] { "1", "2" }, entries.Select(entry => entry.Gid));
        Assert.Equal(2, handler.Requests.Count);
        Assert.DoesNotContain("offset=", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("offset=page2", handler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async Task StopsWhenTheSameOffsetRepeats()
    {
        // Sicherung gegen einen Server, der endlos denselben Zeiger liefert — dieselbe Vorsorge
        // wie in JiraClient gegen ein wiederholtes nextPageToken.
        var handler = new FakeHttpMessageHandler()
            .EnqueueJson(HttpStatusCode.OK, """{"data":[{"gid":"1","duration_minutes":60,"entered_on":"2026-09-21"}],"next_page":{"offset":"same"}}""")
            .EnqueueJson(HttpStatusCode.OK, """{"data":[{"gid":"2","duration_minutes":60,"entered_on":"2026-09-21"}],"next_page":{"offset":"same"}}""");

        var entries = await ClientFor(handler).GetTimeEntriesAsync(
            "111", "900", new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27), CancellationToken.None);

        Assert.Equal(2, entries.Count);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task DeduplicatesEntriesRepeatedAcrossPages()
    {
        var handler = new FakeHttpMessageHandler()
            .EnqueueJson(HttpStatusCode.OK, """{"data":[{"gid":"1","duration_minutes":60,"entered_on":"2026-09-21"}],"next_page":{"offset":"page2"}}""")
            .EnqueueJson(HttpStatusCode.OK, """{"data":[{"gid":"1","duration_minutes":60,"entered_on":"2026-09-21"}],"next_page":null}""");

        var entries = await ClientFor(handler).GetTimeEntriesAsync(
            "111", "900", new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27), CancellationToken.None);

        Assert.Single(entries);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, typeof(AsanaAuthenticationException))]
    [InlineData(HttpStatusCode.PaymentRequired, typeof(AsanaFeatureUnavailableException))]
    [InlineData(HttpStatusCode.Forbidden, typeof(AsanaFeatureUnavailableException))]
    [InlineData(HttpStatusCode.TooManyRequests, typeof(AsanaRateLimitException))]
    [InlineData(HttpStatusCode.InternalServerError, typeof(AsanaUnavailableException))]
    public async Task MapsHttpStatusToException(HttpStatusCode status, Type expected)
    {
        var handler = new FakeHttpMessageHandler().EnqueueJson(status, """{"errors":[{"message":"nope"}]}""");

        var thrown = await Record.ExceptionAsync(() => ClientFor(handler).GetMyselfAsync(CancellationToken.None));

        Assert.IsType(expected, thrown);
    }

    [Fact]
    public async Task UnreadableJsonBecomesUnavailable()
    {
        var handler = new FakeHttpMessageHandler().EnqueueJson(HttpStatusCode.OK, "kein json");

        await Assert.ThrowsAsync<AsanaUnavailableException>(
            () => ClientFor(handler).GetMyselfAsync(CancellationToken.None));
    }

    [Fact]
    public async Task NetworkFailureBecomesUnavailable()
    {
        var handler = new FakeHttpMessageHandler().EnqueueThrow(new HttpRequestException("kein Netz"));

        await Assert.ThrowsAsync<AsanaUnavailableException>(
            () => ClientFor(handler).GetMyselfAsync(CancellationToken.None));
    }

    [Fact]
    public async Task EmptyBodyBecomesUnavailable()
    {
        var handler = new FakeHttpMessageHandler().EnqueueJson(HttpStatusCode.OK, "null");

        await Assert.ThrowsAsync<AsanaUnavailableException>(
            () => ClientFor(handler).GetMyselfAsync(CancellationToken.None));
    }

    [Fact]
    public async Task EndBeforeStartIsRejected()
    {
        var handler = new FakeHttpMessageHandler();

        await Assert.ThrowsAsync<ArgumentException>(() => ClientFor(handler).GetTimeEntriesAsync(
            "111", "900", new DateOnly(2026, 9, 27), new DateOnly(2026, 9, 21), CancellationToken.None));
    }
}
