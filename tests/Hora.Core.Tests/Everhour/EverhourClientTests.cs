using System.Net;
using System.Text;
using Hora.Core.Everhour;
using Hora.Core.Tests.Http;

namespace Hora.Core.Tests.Everhour;

public class EverhourClientTests
{
    private static readonly DateOnly From = new(2026, 11, 2);
    private static readonly DateOnly To = new(2026, 11, 8);

    private static EverhourClient ClientFor(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri(EverhourClient.BaseUrl) });

    /// <summary>Ein Eintrag in der Form, die Everhour am 2026-11-10 tatsächlich geliefert hat.</summary>
    private const string RealEntryJson = """
    {"id":279063192,"date":"2026-11-10","createdAt":"2026-11-10 10:13:28","user":1431001,"time":4800,
     "task":{"id":"as:1220389840924623","name":"Synthara - Strategie","type":"task","status":"open",
             "url":"https://app.asana.com/0/1212635970393658/1220389840924623/f",
             "projects":["as:1212635970393658"],"time":{"total":28200}},
     "history":[],"lockReasons":[],"isLocked":false}
    """;

    [Fact]
    public async Task GetMyselfReadsIdAndName()
    {
        var handler = new FakeHttpMessageHandler().EnqueueJson(HttpStatusCode.OK, """
        {"id":1431001,"name":"Simon Strubel","email":"simon.strubel@advisori.de","role":"member"}
        """);

        var user = await ClientFor(handler).GetMyselfAsync(CancellationToken.None);

        Assert.Equal(1431001, user.Id);
        Assert.Equal("Simon Strubel", user.Name);
        Assert.EndsWith("users/me", Assert.Single(handler.Requests).RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task TimeEntriesSendUserAndBothInclusiveDates()
    {
        var handler = new FakeHttpMessageHandler().EnqueueJson(HttpStatusCode.OK, "[]");

        await ClientFor(handler).GetTimeEntriesAsync(1431001, From, To, CancellationToken.None);

        var url = Assert.Single(handler.Requests).RequestUri!.ToString();
        Assert.Contains("users/1431001/time", url);
        Assert.Contains("from=2026-11-02", url);
        Assert.Contains("to=2026-11-08", url);
        Assert.Contains("limit=1000", url);
    }

    [Fact]
    public async Task MapsTheRealResponseShape()
    {
        var handler = new FakeHttpMessageHandler().EnqueueJson(HttpStatusCode.OK, $"[{RealEntryJson}]");

        var entry = Assert.Single(await ClientFor(handler).GetTimeEntriesAsync(1431001, From, To, CancellationToken.None));

        Assert.Equal(279063192, entry.Id);
        Assert.Equal(new DateOnly(2026, 11, 10), entry.Date);
        Assert.Equal(4800, entry.Time);
        Assert.Null(entry.Comment);
        Assert.Equal("as:1220389840924623", entry.Task!.Id);
        Assert.Equal("Synthara - Strategie", entry.Task.Name);
        Assert.Equal("https://app.asana.com/0/1212635970393658/1220389840924623/f", entry.Task.Url);
        Assert.Equal("as:1212635970393658", Assert.Single(entry.Task.Projects));
    }

    [Fact]
    public async Task ReadsCommentAndEntriesWithoutTask()
    {
        var handler = new FakeHttpMessageHandler().EnqueueJson(HttpStatusCode.OK, """
        [{"id":1,"date":"2026-11-03","time":1800,"comment":"[SYN-2204#wl-42234]","task":null}]
        """);

        var entry = Assert.Single(await ClientFor(handler).GetTimeEntriesAsync(1431001, From, To, CancellationToken.None));

        Assert.Equal("[SYN-2204#wl-42234]", entry.Comment);
        Assert.Null(entry.Task);
    }

    /// <summary>Derselbe Eintrag zweimal in der Antwort darf seine Zeit nicht zweimal liefern.</summary>
    [Fact]
    public async Task DeduplicatesEntriesById()
    {
        var handler = new FakeHttpMessageHandler().EnqueueJson(HttpStatusCode.OK, $"[{RealEntryJson},{RealEntryJson}]");

        Assert.Single(await ClientFor(handler).GetTimeEntriesAsync(1431001, From, To, CancellationToken.None));
    }

    /// <summary>
    /// Everhour blättert hier nicht weiter. Eine Antwort, die die Obergrenze erreicht, ist
    /// möglicherweise abgeschnitten — still zu wenig anzuzeigen wäre schlimmer als eine Meldung.
    /// </summary>
    [Fact]
    public async Task FullPageIsReportedAsPossiblyIncomplete()
    {
        var json = new StringBuilder("[");
        for (var id = 1; id <= EverhourClient.MaxEntries; id++)
        {
            json.Append(id > 1 ? "," : string.Empty)
                .Append("{\"id\":").Append(id).Append(",\"date\":\"2026-11-03\",\"time\":60}");
        }

        var handler = new FakeHttpMessageHandler().EnqueueJson(HttpStatusCode.OK, json.Append(']').ToString());

        await Assert.ThrowsAsync<EverhourUnavailableException>(
            () => ClientFor(handler).GetTimeEntriesAsync(1431001, From, To, CancellationToken.None));
    }

    [Fact]
    public async Task ProjectLookupEscapesThePlatformPrefix()
    {
        var handler = new FakeHttpMessageHandler().EnqueueJson(HttpStatusCode.OK, """
        {"id":"as:1212635970393658","platform":"as","name":"2026-I-02|DT|-interne-Meetings"}
        """);

        var project = await ClientFor(handler).GetProjectAsync("as:1212635970393658", CancellationToken.None);

        Assert.Equal("2026-I-02|DT|-interne-Meetings", project.Name);
        Assert.Contains("projects/as%3A1212635970393658", Assert.Single(handler.Requests).RequestUri!.OriginalString);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, typeof(EverhourAuthenticationException))]
    [InlineData(HttpStatusCode.TooManyRequests, typeof(EverhourRateLimitException))]
    [InlineData(HttpStatusCode.Forbidden, typeof(EverhourUnavailableException))]
    [InlineData(HttpStatusCode.InternalServerError, typeof(EverhourUnavailableException))]
    public async Task MapsHttpStatusToException(HttpStatusCode status, Type expected)
    {
        var handler = new FakeHttpMessageHandler().EnqueueJson(status, """{"code":0,"message":"nope"}""");

        var thrown = await Record.ExceptionAsync(() => ClientFor(handler).GetMyselfAsync(CancellationToken.None));

        Assert.IsType(expected, thrown);
    }

    [Fact]
    public async Task UnreadableJsonBecomesUnavailable()
    {
        var handler = new FakeHttpMessageHandler().EnqueueJson(HttpStatusCode.OK, "kein json");

        await Assert.ThrowsAsync<EverhourUnavailableException>(
            () => ClientFor(handler).GetMyselfAsync(CancellationToken.None));
    }

    [Fact]
    public async Task NetworkFailureBecomesUnavailable()
    {
        var handler = new FakeHttpMessageHandler().EnqueueThrow(new HttpRequestException("kein Netz"));

        await Assert.ThrowsAsync<EverhourUnavailableException>(
            () => ClientFor(handler).GetMyselfAsync(CancellationToken.None));
    }

    [Fact]
    public async Task EmptyBodyBecomesUnavailable()
    {
        var handler = new FakeHttpMessageHandler().EnqueueJson(HttpStatusCode.OK, "null");

        await Assert.ThrowsAsync<EverhourUnavailableException>(
            () => ClientFor(handler).GetMyselfAsync(CancellationToken.None));
    }

    [Fact]
    public async Task EndBeforeStartIsRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => ClientFor(new FakeHttpMessageHandler())
            .GetTimeEntriesAsync(1431001, To, From, CancellationToken.None));
    }
}
