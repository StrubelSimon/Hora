using System.Globalization;
using System.Text.Json;
using Hora.Core.Everhour.Models;
using Hora.Core.Http;

namespace Hora.Core.Everhour;

/// <summary>HTTP-Zugriff auf die Everhour-API, ausschließlich lesend.</summary>
public sealed class EverhourClient : IEverhourClient
{
    public const string BaseUrl = "https://api.everhour.com/";

    /// <summary>
    /// Obergrenze je Abruf. Everhour blättert diesen Endpunkt nicht; ein Nutzer hat höchstens einen
    /// Eintrag je Tag und Aufgabe und erreicht in einem Monat nie annähernd 1000.
    /// </summary>
    public const int MaxEntries = 1000;

    /// <summary>Everhour liefert camelCase — genau das, was die Web-Voreinstellung erwartet.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    /// <summary>Erwartet einen HttpClient mit gesetzter BaseAddress (mit abschließendem Schrägstrich).</summary>
    public EverhourClient(HttpClient http)
    {
        _http = http;
    }

    public static EverhourClient Create(string apiKey)
    {
        var pipeline = new ApiKeyAuthHandler(apiKey)
        {
            InnerHandler = new RateLimitRetryHandler
            {
                InnerHandler = new SocketsHttpHandler(),
            },
        };

        var http = new HttpClient(pipeline)
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(30),
        };

        return new EverhourClient(http);
    }

    public Task<EverhourUser> GetMyselfAsync(CancellationToken cancellationToken) =>
        GetJsonAsync<EverhourUser>("users/me", cancellationToken);

    public async Task<IReadOnlyList<EverhourTimeEntry>> GetTimeEntriesAsync(
        long userId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            throw new ArgumentException("Das Enddatum liegt vor dem Startdatum.", nameof(to));
        }

        var url = $"users/{userId.ToString(CultureInfo.InvariantCulture)}/time" +
                  $"?from={from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}" +
                  $"&to={to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}" +
                  $"&limit={MaxEntries}";

        var page = await GetJsonAsync<List<EverhourTimeEntry>>(url, cancellationToken);

        // Eine volle Antwort kann abgeschnitten sein. Still weniger anzuzeigen hieße, Zeiten zu
        // verschweigen — dann lieber eine Meldung und gar keine Everhour-Zahlen.
        if (page.Count >= MaxEntries)
        {
            throw new EverhourUnavailableException(
                $"Everhour lieferte für {from:yyyy-MM-dd} bis {to:yyyy-MM-dd} die Höchstzahl von {MaxEntries} Einträgen; die Antwort ist möglicherweise unvollständig.");
        }

        // Entdopplung nach Kennung: Derselbe Eintrag zweimal hieße dieselbe Zeit zweimal gezählt.
        var seenIds = new HashSet<long>();
        return page.Where(entry => seenIds.Add(entry.Id)).ToList();
    }

    public Task<EverhourProject> GetProjectAsync(string projectId, CancellationToken cancellationToken) =>
        GetJsonAsync<EverhourProject>("projects/" + Uri.EscapeDataString(projectId), cancellationToken);

    private async Task<T> GetJsonAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        // Anfrage, Rumpf-Lesen und Deserialisierung in einem try: Wegen ResponseHeadersRead kommt
        // der Rumpf erst beim Lesen über das Netz, ein Abriss dabei ist ein Erreichbarkeitsproblem.
        try
        {
            using var response = await _http.GetAsync(relativeUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            ThrowIfError(response, relativeUrl);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
            return result ?? throw new EverhourUnavailableException($"Leere Antwort von Everhour auf {relativeUrl}.");
        }
        catch (HttpRequestException ex)
        {
            throw new EverhourUnavailableException("Everhour ist nicht erreichbar.", innerException: ex);
        }
        catch (IOException ex)
        {
            throw new EverhourUnavailableException("Die Verbindung zu Everhour wurde beim Lesen der Antwort unterbrochen.", innerException: ex);
        }
        catch (JsonException ex)
        {
            throw new EverhourUnavailableException($"Everhour hat eine unlesbare Antwort auf {relativeUrl} geliefert.", innerException: ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EverhourUnavailableException("Zeitüberschreitung bei der Anfrage an Everhour.", innerException: ex);
        }
    }

    private static void ThrowIfError(HttpResponseMessage response, string relativeUrl)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var status = (int)response.StatusCode;

        // Nur 401 heißt „Schlüssel ungültig“ und löst das Verwerfen der Verbindung aus. Ein 403
        // betrifft einen einzelnen Endpunkt (etwa teamweite Auswertungen), nicht den Schlüssel.
        if (status == 401)
        {
            throw new EverhourAuthenticationException();
        }

        if (status == 429)
        {
            throw new EverhourRateLimitException();
        }

        throw new EverhourUnavailableException($"Everhour antwortete mit HTTP {status} auf {relativeUrl}.", status);
    }
}
