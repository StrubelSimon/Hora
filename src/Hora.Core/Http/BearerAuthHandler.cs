using System.Net.Http.Headers;

namespace Hora.Core.Http;

/// <summary>Setzt den Asana-Token als Bearer und Accept: application/json auf jede Anfrage.</summary>
public sealed class BearerAuthHandler : DelegatingHandler
{
    private readonly string _token;

    public BearerAuthHandler(string token)
    {
        _token = token;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return base.SendAsync(request, cancellationToken);
    }
}
