using System.Net.Http.Headers;
using System.Text;

namespace Hora.Core.Jira;

/// <summary>Setzt Basic Auth (E-Mail:API-Token) und Accept: application/json auf jede Anfrage.</summary>
public sealed class BasicAuthHandler : DelegatingHandler
{
    private readonly string _encodedCredentials;

    public BasicAuthHandler(string email, string apiToken)
    {
        _encodedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _encodedCredentials);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return base.SendAsync(request, cancellationToken);
    }
}
