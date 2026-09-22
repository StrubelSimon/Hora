using System.Globalization;
using System.Text.Json;
using Hora.Core.Jira.Models;

namespace Hora.Core.Jira;

/// <summary>HTTP-Zugriff auf die Jira Cloud REST API v3 inklusive Pagination.</summary>
public sealed class JiraClient : IJiraClient
{
    private const int PageSize = 100;

    // Sicherheitsnetz gegen einen fehlerhaften Server oder Zwischenproxy, der die Suche endlos
    // weiterblättern lässt. 50 Seiten zu je 100 Tickets sind für einen einzelnen Nutzer über einen
    // neuntägigen Zeitraum (die übliche Fensterbreite) um Größenordnungen mehr als realistisch.
    private const int MaxSearchPages = 50;

    private readonly HttpClient _http;

    /// <summary>Erwartet einen HttpClient mit gesetzter BaseAddress (mit abschließendem Schrägstrich).</summary>
    public JiraClient(HttpClient http)
    {
        _http = http;
    }

    public static JiraClient Create(string baseUrl, string email, string apiToken)
    {
        var pipeline = new BasicAuthHandler(email, apiToken)
        {
            InnerHandler = new RateLimitRetryHandler
            {
                InnerHandler = new SocketsHttpHandler(),
            },
        };

        var http = new HttpClient(pipeline)
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30),
        };

        return new JiraClient(http);
    }

    public Task<JiraUser> GetMyselfAsync(CancellationToken cancellationToken) =>
        GetJsonAsync<JiraUser>("rest/api/3/myself", cancellationToken);

    public async Task<IReadOnlyList<JiraIssue>> SearchIssuesWithWorklogsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        // worklogDate wird in der Jira-Profil-Zeitzone ausgewertet; ein Tag Puffer auf jeder Seite fängt Zeitzonenkanten ab.
        var jql = string.Format(CultureInfo.InvariantCulture,
            "worklogAuthor = currentUser() AND worklogDate >= \"{0:yyyy-MM-dd}\" AND worklogDate <= \"{1:yyyy-MM-dd}\"",
            from.AddDays(-1), to.AddDays(1));

        var issues = new List<JiraIssue>();
        // Entdopplung nach Ticket-Schlüssel: Jira kann dasselbe Ticket auf zwei Seiten liefern, wenn
        // sich die Ergebnismenge zwischen den Seitenabrufen verschiebt. Ohne diese Menge würden dessen
        // Buchungen zweimal abgerufen und doppelt gezählt (Befund D).
        var seenIssueKeys = new HashSet<string>(StringComparer.Ordinal);
        string? nextPageToken = null;
        var pageCount = 0;

        while (true)
        {
            var url = $"rest/api/3/search/jql?jql={Uri.EscapeDataString(jql)}&fields=summary&maxResults={PageSize}";
            if (nextPageToken is not null)
            {
                url += $"&nextPageToken={Uri.EscapeDataString(nextPageToken)}";
            }

            var page = await GetJsonAsync<SearchResponse>(url, cancellationToken);
            foreach (var issue in page.Issues)
            {
                if (seenIssueKeys.Add(issue.Key))
                {
                    issues.Add(issue);
                }
            }

            pageCount++;

            // Wiederholt die Antwort dasselbe Token wie die gerade abgerufene Seite, liefert Jira
            // keine neuen Daten mehr – weiterblättern würde nur dieselbe Seite erneut anfordern.
            var isRepeatedToken = page.NextPageToken is not null && page.NextPageToken == nextPageToken;
            var hasMore = page.IsLast != true && !string.IsNullOrEmpty(page.NextPageToken) && page.Issues.Count > 0 && !isRepeatedToken;
            if (!hasMore)
            {
                break;
            }

            if (pageCount >= MaxSearchPages)
            {
                // Zweite, unabhängige Sicherung: selbst mit stets wechselnden Token ist bei einem
                // einzelnen Nutzer über wenige Tage diese Seitenzahl nie im laufenden Betrieb erreicht.
                // Wir brechen sichtbar ab statt die Ergebnisliste stillschweigend zu kappen.
                throw new JiraUnavailableException(
                    $"Die Ticketsuche hat die Sicherheitsgrenze von {MaxSearchPages} Seiten erreicht, ohne dass Jira das Ende der Ergebnisse gemeldet hat.");
            }

            nextPageToken = page.NextPageToken;
        }

        return issues;
    }

    public async Task<IReadOnlyList<JiraWorklog>> GetWorklogsAsync(string issueKey, DateTimeOffset startedAfter, DateTimeOffset startedBefore, CancellationToken cancellationToken)
    {
        var worklogs = new List<JiraWorklog>();
        var startAt = 0;

        while (true)
        {
            var url = $"rest/api/3/issue/{Uri.EscapeDataString(issueKey)}/worklog" +
                      $"?startedAfter={startedAfter.ToUnixTimeMilliseconds()}" +
                      $"&startedBefore={startedBefore.ToUnixTimeMilliseconds()}" +
                      $"&startAt={startAt}&maxResults={PageSize}";

            var page = await GetJsonAsync<PageOfWorklogs>(url, cancellationToken);
            worklogs.AddRange(page.Worklogs);
            startAt += page.Worklogs.Count;

            if (page.Worklogs.Count == 0 || startAt >= page.Total)
            {
                break;
            }
        }

        return worklogs;
    }

    private async Task<T> GetJsonAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        // Anfrage, Rumpf-Lesen und Deserialisierung liegen bewusst in einem gemeinsamen try: Wegen
        // ResponseHeadersRead unten kommt der Rumpf erst beim Lesen über das Netz, ein Abriss dabei
        // ist also genauso ein Jira-Erreichbarkeitsproblem wie ein Fehler bei GetAsync selbst.
        try
        {
            using var response = await _http.GetAsync(relativeUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            ThrowIfError(response, relativeUrl);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await JsonSerializer.DeserializeAsync<T>(stream, JiraJson.Options, cancellationToken);
            return result ?? throw new JiraUnavailableException($"Leere Antwort von Jira auf {relativeUrl}.");
        }
        catch (HttpRequestException ex)
        {
            throw new JiraUnavailableException("Jira ist nicht erreichbar.", innerException: ex);
        }
        catch (IOException ex)
        {
            // Verbindungsabriss beim Lesen des Rumpfes, z. B. im Firmennetz oder auf Mobilfunk.
            throw new JiraUnavailableException("Die Verbindung zu Jira wurde beim Lesen der Antwort unterbrochen.", innerException: ex);
        }
        catch (JsonException ex)
        {
            throw new JiraUnavailableException($"Jira hat eine unlesbare Antwort auf {relativeUrl} geliefert.", innerException: ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient meldet eine Zeitüberschreitung als TaskCanceledException.
            throw new JiraUnavailableException("Zeitüberschreitung bei der Anfrage an Jira.", innerException: ex);
        }
    }

    private static void ThrowIfError(HttpResponseMessage response, string relativeUrl)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var status = (int)response.StatusCode;
        if (status is 401 or 403)
        {
            throw new JiraAuthenticationException();
        }

        if (status == 429)
        {
            throw new JiraRateLimitException();
        }

        throw new JiraUnavailableException($"Jira antwortete mit HTTP {status} auf {relativeUrl}.", status);
    }
}
