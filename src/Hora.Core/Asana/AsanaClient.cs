using System.Globalization;
using System.Text.Json;
using Hora.Core.Asana.Models;
using Hora.Core.Http;

namespace Hora.Core.Asana;

/// <summary>HTTP-Zugriff auf die Asana REST API 1.0 inklusive seitenweisem Abruf.</summary>
public sealed class AsanaClient : IAsanaClient
{
    public const string BaseUrl = "https://app.asana.com/api/1.0/";

    /// <summary>Asanas Höchstwert; größere Werte lehnt der Dienst ab.</summary>
    private const int PageSize = 100;

    /// <summary>
    /// Sicherheitsnetz gegen einen Server, der endlos weiterblättern lässt. Ein einzelner Nutzer
    /// erreicht über wenige Wochen nie 50 Seiten zu je 100 Einträgen.
    /// </summary>
    private const int MaxPages = 50;

    /// <summary>
    /// Die Felder, die der Listenabruf mitbringen soll.
    /// </summary>
    /// <remarks>
    /// Asanas OpenAPI-Spezifikation führt für diesen Endpunkt TimeTrackingEntryCompact, und das
    /// kennt keinen Task — `task.name` und `task.permalink_url` standen deshalb zunächst nicht hier.
    /// Eine Probe gegen die echte API hat das widerlegt: Über opt_fields kommen beide zuverlässig
    /// mit. Und sie sind nicht bloß Beiwerk, sondern das Tragende. Bei 36 Einträgen eines echten
    /// Workspace trugen alle 36 einen Task-Namen und einen Permalink, aber nur 3 einen Projektnamen
    /// und kein einziger eine Kategorie. Ohne diese beiden Felder bliebe die Titelspalte also fast
    /// immer leer.
    /// </remarks>
    private const string TimeEntryFields =
        "duration_minutes,entered_on,attributable_to.name,categories.name,created_by,task.name,task.permalink_url";

    private readonly HttpClient _http;

    /// <summary>Erwartet einen HttpClient mit gesetzter BaseAddress (mit abschließendem Schrägstrich).</summary>
    public AsanaClient(HttpClient http)
    {
        _http = http;
    }

    public static AsanaClient Create(string token)
    {
        var pipeline = new BearerAuthHandler(token)
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

        return new AsanaClient(http);
    }

    public async Task<AsanaUser> GetMyselfAsync(CancellationToken cancellationToken)
    {
        var url = "users/me?opt_fields=" + Uri.EscapeDataString("name,email,workspaces.name");
        var response = await GetJsonAsync<AsanaResponse<AsanaUser>>(url, cancellationToken);
        return response.Data ?? throw new AsanaUnavailableException("Asana lieferte keine Nutzerdaten.");
    }

    public async Task<IReadOnlyList<AsanaTimeEntry>> GetTimeEntriesAsync(
        string userGid,
        string workspaceGid,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            throw new ArgumentException("Das Enddatum liegt vor dem Startdatum.", nameof(to));
        }

        var entries = new List<AsanaTimeEntry>();
        // Entdopplung nach Kennung: Verschiebt sich die Ergebnismenge zwischen zwei Seitenabrufen,
        // kann Asana denselben Eintrag zweimal liefern. Ohne diese Menge zählte seine Zeit doppelt —
        // derselbe Fehler, der bei Jira als Befund D behoben wurde.
        var seenGids = new HashSet<string>(StringComparer.Ordinal);
        string? offset = null;
        var pageCount = 0;

        while (true)
        {
            var url = "time_tracking_entries" +
                      $"?user={Uri.EscapeDataString(userGid)}" +
                      $"&workspace={Uri.EscapeDataString(workspaceGid)}" +
                      $"&entered_on_start_date={from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}" +
                      $"&entered_on_end_date={to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}" +
                      $"&limit={PageSize}" +
                      $"&opt_fields={Uri.EscapeDataString(TimeEntryFields)}";

            if (offset is not null)
            {
                url += $"&offset={Uri.EscapeDataString(offset)}";
            }

            var page = await GetJsonAsync<AsanaResponse<List<AsanaTimeEntry>>>(url, cancellationToken);
            foreach (var entry in page.Data ?? new List<AsanaTimeEntry>())
            {
                if (seenGids.Add(entry.Gid))
                {
                    entries.Add(entry);
                }
            }

            pageCount++;

            var nextOffset = page.NextPage?.Offset;
            // Wiederholt Asana denselben Zeiger, kommen keine neuen Daten mehr; weiterblättern
            // forderte nur dieselbe Seite erneut an.
            var hasMore = !string.IsNullOrEmpty(nextOffset) && nextOffset != offset;
            if (!hasMore)
            {
                break;
            }

            if (pageCount >= MaxPages)
            {
                throw new AsanaUnavailableException(
                    $"Der Abruf der Zeiteinträge hat die Sicherheitsgrenze von {MaxPages} Seiten erreicht, ohne dass Asana das Ende gemeldet hat.");
            }

            offset = nextOffset;
        }

        return entries;
    }

    private async Task<T> GetJsonAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        // Anfrage, Rumpf-Lesen und Deserialisierung liegen in einem gemeinsamen try: Wegen
        // ResponseHeadersRead kommt der Rumpf erst beim Lesen über das Netz, ein Abriss dabei ist
        // also genauso ein Erreichbarkeitsproblem wie ein Fehler bei GetAsync selbst.
        try
        {
            using var response = await _http.GetAsync(relativeUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            ThrowIfError(response, relativeUrl);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await JsonSerializer.DeserializeAsync<T>(stream, AsanaJson.Options, cancellationToken);
            return result ?? throw new AsanaUnavailableException($"Leere Antwort von Asana auf {relativeUrl}.");
        }
        catch (HttpRequestException ex)
        {
            throw new AsanaUnavailableException("Asana ist nicht erreichbar.", innerException: ex);
        }
        catch (IOException ex)
        {
            throw new AsanaUnavailableException("Die Verbindung zu Asana wurde beim Lesen der Antwort unterbrochen.", innerException: ex);
        }
        catch (JsonException ex)
        {
            throw new AsanaUnavailableException($"Asana hat eine unlesbare Antwort auf {relativeUrl} geliefert.", innerException: ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AsanaUnavailableException("Zeitüberschreitung bei der Anfrage an Asana.", innerException: ex);
        }
    }

    private static void ThrowIfError(HttpResponseMessage response, string relativeUrl)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var status = (int)response.StatusCode;

        // 401 und 403 werden bewusst getrennt: 401 heißt „Token ungültig" und löst das Verwerfen
        // der Verbindung aus, 403 heißt bei Asana in aller Regel „Funktion im Tarif nicht
        // enthalten" und muss die Verbindung stehen lassen.
        if (status == 401)
        {
            throw new AsanaAuthenticationException();
        }

        if (status is 402 or 403)
        {
            throw new AsanaFeatureUnavailableException();
        }

        if (status == 429)
        {
            throw new AsanaRateLimitException();
        }

        throw new AsanaUnavailableException($"Asana antwortete mit HTTP {status} auf {relativeUrl}.", status);
    }
}
