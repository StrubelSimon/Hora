using System.Net;
using System.Net.Http.Headers;
using Hora.Core.Jira;

namespace Hora.Core.Tests.Jira;

public class RateLimitRetryHandlerTests
{
    private static (HttpClient client, FakeHttpMessageHandler fake, List<TimeSpan> delays) Create()
    {
        var fake = new FakeHttpMessageHandler();
        var delays = new List<TimeSpan>();
        var handler = new RateLimitRetryHandler((delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        })
        {
            InnerHandler = fake,
        };
        return (new HttpClient(handler), fake, delays);
    }

    private static void RetryAfterSeconds(HttpResponseMessage response, int seconds) =>
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));

    private static void RetryAfterDate(HttpResponseMessage response, DateTimeOffset date) =>
        response.Headers.RetryAfter = new RetryConditionHeaderValue(date);

    [Fact]
    public async Task Passes_success_through_without_delay()
    {
        var (client, fake, delays) = Create();
        fake.EnqueueJson(HttpStatusCode.OK, "{}");

        var response = await client.GetAsync("https://example.atlassian.net/x");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(fake.Requests);
        Assert.Empty(delays);
    }

    [Fact]
    public async Task Retries_once_after_429_using_retry_after()
    {
        var (client, fake, delays) = Create();
        fake.EnqueueJson(HttpStatusCode.TooManyRequests, "{}", response => RetryAfterSeconds(response, 3));
        fake.EnqueueJson(HttpStatusCode.OK, "{}");

        var response = await client.GetAsync("https://example.atlassian.net/x");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, fake.Requests.Count);
        Assert.Equal(new[] { TimeSpan.FromSeconds(3) }, delays);
    }

    [Fact]
    public async Task Waits_two_seconds_when_retry_after_is_missing()
    {
        var (client, fake, delays) = Create();
        fake.EnqueueJson(HttpStatusCode.TooManyRequests, "{}");
        fake.EnqueueJson(HttpStatusCode.OK, "{}");

        await client.GetAsync("https://example.atlassian.net/x");

        Assert.Equal(new[] { TimeSpan.FromSeconds(2) }, delays);
    }

    [Fact]
    public async Task Caps_delay_at_thirty_seconds()
    {
        var (client, fake, delays) = Create();
        fake.EnqueueJson(HttpStatusCode.TooManyRequests, "{}", response => RetryAfterSeconds(response, 120));
        fake.EnqueueJson(HttpStatusCode.OK, "{}");

        await client.GetAsync("https://example.atlassian.net/x");

        Assert.Equal(new[] { TimeSpan.FromSeconds(30) }, delays);
    }

    [Fact]
    public async Task Returns_second_429_without_further_retry()
    {
        var (client, fake, _) = Create();
        fake.EnqueueJson(HttpStatusCode.TooManyRequests, "{}");
        fake.EnqueueJson(HttpStatusCode.TooManyRequests, "{}");

        var response = await client.GetAsync("https://example.atlassian.net/x");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(2, fake.Requests.Count);
    }

    [Fact]
    public async Task Retried_request_keeps_headers_of_the_original()
    {
        var (client, fake, _) = Create();
        fake.EnqueueJson(HttpStatusCode.TooManyRequests, "{}");
        fake.EnqueueJson(HttpStatusCode.OK, "{}");
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.atlassian.net/x");
        request.Headers.TryAddWithoutValidation("X-Test", "wert");

        await client.SendAsync(request);

        Assert.Equal("wert", fake.Requests[1].Headers.GetValues("X-Test").Single());
    }

    [Fact]
    public async Task Retries_after_future_retry_after_date_using_remaining_time()
    {
        var (client, fake, delays) = Create();
        fake.EnqueueJson(HttpStatusCode.TooManyRequests, "{}", response => RetryAfterDate(response, DateTimeOffset.UtcNow.AddSeconds(10)));
        fake.EnqueueJson(HttpStatusCode.OK, "{}");

        await client.GetAsync("https://example.atlassian.net/x");

        // Bereich statt exaktem Wert, weil zwischen Setzen des Datums und Auswertung Zeit verstreicht.
        var delay = Assert.Single(delays);
        Assert.InRange(delay, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Retries_immediately_when_retry_after_date_is_in_the_past()
    {
        var (client, fake, delays) = Create();
        fake.EnqueueJson(HttpStatusCode.TooManyRequests, "{}", response => RetryAfterDate(response, DateTimeOffset.UtcNow.AddSeconds(-30)));
        fake.EnqueueJson(HttpStatusCode.OK, "{}");

        await client.GetAsync("https://example.atlassian.net/x");

        Assert.Equal(new[] { TimeSpan.Zero }, delays);
    }

    [Fact]
    public async Task Retry_with_request_body_fails_loudly_instead_of_silently_dropping_it()
    {
        var (client, fake, _) = Create();
        fake.EnqueueJson(HttpStatusCode.TooManyRequests, "{}");
        fake.EnqueueJson(HttpStatusCode.OK, "{}");
        var request = new HttpRequestMessage(HttpMethod.Post, "https://example.atlassian.net/x")
        {
            Content = new StringContent("payload"),
        };

        await Assert.ThrowsAsync<NotSupportedException>(() => client.SendAsync(request));
    }
}
