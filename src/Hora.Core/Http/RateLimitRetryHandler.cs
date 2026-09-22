using System.Net;

namespace Hora.Core.Http;

/// <summary>Wiederholt eine Anfrage bei HTTP 429 genau einmal nach Retry-After (Standard 2 s, maximal 30 s).</summary>
public sealed class RateLimitRetryHandler : DelegatingHandler
{
    private static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public RateLimitRetryHandler()
        : this((delay, cancellationToken) => Task.Delay(delay, cancellationToken))
    {
    }

    /// <summary>Für Tests: erlaubt, das Warten zu ersetzen.</summary>
    public RateLimitRetryHandler(Func<TimeSpan, CancellationToken, Task> delay)
    {
        _delay = delay;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.TooManyRequests)
        {
            return response;
        }

        var delay = ReadRetryAfter(response) ?? DefaultDelay;
        if (delay > MaxDelay)
        {
            delay = MaxDelay;
        }

        response.Dispose();
        await _delay(delay, cancellationToken);

        // Eine HttpRequestMessage darf nicht zweimal gesendet werden, deshalb eine Kopie.
        return await base.SendAsync(Clone(request), cancellationToken);
    }

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var untilDate = date - DateTimeOffset.UtcNow;
            return untilDate > TimeSpan.Zero ? untilDate : TimeSpan.Zero;
        }

        return null;
    }

    private static HttpRequestMessage Clone(HttpRequestMessage request)
    {
        // Hora sendet heute nur GET ohne Rumpf, deshalb kopiert diese einfache Klon-Logik nur
        // Method, RequestUri und Header. Ein Rumpf (z. B. bei einem künftigen POST/PUT) ließe sich
        // nicht immer zuverlässig kopieren (etwa bei bereits gelesenen Streams) und würde hier sonst
        // stillschweigend verloren gehen. Deshalb lieber laut scheitern als Daten unbemerkt zu verlieren.
        if (request.Content is not null)
        {
            throw new NotSupportedException(
                "RateLimitRetryHandler kann eine Wiederholung nach HTTP 429 nicht sicher durchführen, " +
                "weil die Anfrage einen Rumpf (Content) trägt. Falls künftig ein POST/PUT mit Rumpf " +
                "eingeführt wird, muss Clone() zuerst um eine zuverlässige Kopie des Inhalts erweitert werden.");
        }

        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
