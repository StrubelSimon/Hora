using System.Net.Http.Headers;

namespace Hora.Core.Http;

/// <summary>Setzt den Everhour-Schlüssel als X-Api-Key und Accept: application/json auf jede Anfrage.</summary>
public sealed class ApiKeyAuthHandler : DelegatingHandler
{
    public const string HeaderName = "X-Api-Key";

    private readonly string _apiKey;

    public ApiKeyAuthHandler(string apiKey)
    {
        _apiKey = apiKey;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove(HeaderName);
        request.Headers.Add(HeaderName, _apiKey);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return base.SendAsync(request, cancellationToken);
    }
}
