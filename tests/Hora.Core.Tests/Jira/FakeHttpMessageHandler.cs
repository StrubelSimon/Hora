using System.Net;
using System.Text;

namespace Hora.Core.Tests.Jira;

/// <summary>Liefert vorbereitete Antworten in Reihenfolge und merkt sich alle Anfragen.</summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responders = new();

    public List<HttpRequestMessage> Requests { get; } = new();

    public FakeHttpMessageHandler EnqueueJson(HttpStatusCode status, string json, Action<HttpResponseMessage>? configure = null)
    {
        _responders.Enqueue(_ =>
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            configure?.Invoke(response);
            return response;
        });
        return this;
    }

    public FakeHttpMessageHandler EnqueueThrow(Exception exception)
    {
        _responders.Enqueue(_ => throw exception);
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Wie ein echter Handler: ein bereits abgebrochener Token führt zum Abbruch.
        cancellationToken.ThrowIfCancellationRequested();

        Requests.Add(request);
        if (_responders.Count == 0)
        {
            throw new InvalidOperationException($"Keine Antwort vorbereitet für {request.Method} {request.RequestUri}");
        }

        return Task.FromResult(_responders.Dequeue()(request));
    }
}
