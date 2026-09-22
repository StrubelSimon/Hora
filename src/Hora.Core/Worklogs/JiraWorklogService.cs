using System.Collections.Concurrent;
using Hora.Core.Jira;
using Hora.Core.Jira.Models;
using Hora.Core.Logging;

namespace Hora.Core.Worklogs;

/// <summary>
/// Sucht per JQL die Tickets mit eigenen Buchungen im Zeitraum, lädt deren Worklogs parallel
/// (höchstens vier gleichzeitig) und filtert auf den eigenen Account und die lokalen Tagesgrenzen.
/// Der Jira-Anbieter hinter <see cref="IWorklogService"/>.
/// </summary>
public sealed class JiraWorklogService : IWorklogService
{
    private const int MaxParallelRequests = 4;

    private readonly IJiraClient _jira;
    private readonly string _accountId;
    private readonly string _baseUrl;
    private readonly TimeZoneInfo _zone;
    private readonly IAppLogger _logger;

    public JiraWorklogService(IJiraClient jira, string accountId, string baseUrl, TimeZoneInfo zone, IAppLogger logger)
    {
        _jira = jira;
        _accountId = accountId;
        _baseUrl = baseUrl;
        _zone = zone;
        _logger = logger;
    }

    public async Task<WorklogFetchResult> GetEntriesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from)
        {
            throw new ArgumentException("Das Enddatum liegt vor dem Startdatum.", nameof(to));
        }

        var issues = await _jira.SearchIssuesWithWorklogsAsync(from, to, cancellationToken);

        var rangeStart = LocalMidnight(from);           // inklusive
        var rangeEnd = LocalMidnight(to.AddDays(1));    // exklusive
        var startedAfter = rangeStart.AddMilliseconds(-1);

        var entries = new ConcurrentBag<WorklogEntry>();
        var failedIssueKeys = new ConcurrentBag<string>();
        // Bewusst ohne using: Bricht ein Task mit 401 ab, laufen die übrigen noch kurz weiter
        // und geben die Semaphore frei. Ohne AvailableWaitHandle braucht SemaphoreSlim kein Dispose.
        var gate = new SemaphoreSlim(MaxParallelRequests);

        var tasks = issues.Select(async issue =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var worklogs = await _jira.GetWorklogsAsync(issue.Key, startedAfter, rangeEnd, cancellationToken);
                foreach (var worklog in worklogs)
                {
                    if (!IsOwn(worklog))
                    {
                        continue;
                    }

                    var started = JiraDateParser.Parse(worklog.Started);
                    if (started < rangeStart || started >= rangeEnd)
                    {
                        continue;
                    }

                    entries.Add(WorklogEntry.Jira(
                        issue.Key,
                        issue.Fields.Summary,
                        started,
                        TimeSpan.FromSeconds(worklog.TimeSpentSeconds),
                        AdfTextExtractor.Extract(worklog.Comment),
                        _baseUrl));
                }
            }
            catch (JiraAuthenticationException)
            {
                throw; // Ohne gültige Anmeldung ist der ganze Abruf sinnlos.
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is JiraException or FormatException)
            {
                _logger.Error($"Worklogs für {issue.Key} konnten nicht geladen werden", ex);
                failedIssueKeys.Add(issue.Key);
            }
            catch (Exception ex)
            {
                // Auffangzweig als letzter, hinter den beiden durchreichenden Zweigen: Liefert Jira
                // etwa "fields": null, wirft der Zugriff auf den Titel eine NullReferenceException.
                // Ohne diesen Zweig entkäme sie aus Task.WhenAll und beendete den gesamten Abruf,
                // statt dieses eine Ticket als Teilfehler zu melden (Befund E).
                _logger.Error($"Worklogs für {issue.Key} konnten unerwartet nicht geladen werden", ex);
                failedIssueKeys.Add(issue.Key);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);

        // Zweiter Sortierschlüssel nach Ticket-Schlüssel: Die Sortierung ist zwar stabil, die
        // Aufzählungsreihenfolge der nebenläufig gefüllten ConcurrentBag davor aber nicht. Ohne ihn
        // tauschen zwei Buchungen mit identischem Zeitstempel bei jedem Aktualisieren die Plätze
        // (Befund G).
        return new WorklogFetchResult(
            entries
                .OrderBy(entry => entry.Started)
                .ThenBy(entry => entry.ItemKey, StringComparer.Ordinal)
                .ToList(),
            failedIssueKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
            Array.Empty<string>());
    }

    private bool IsOwn(JiraWorklog worklog) =>
        string.Equals(worklog.Author?.AccountId, _accountId, StringComparison.Ordinal);

    private DateTimeOffset LocalMidnight(DateOnly date)
    {
        var localClock = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(localClock, _zone.GetUtcOffset(localClock));
    }
}
