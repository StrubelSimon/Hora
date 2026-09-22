using Hora.Core.Jira.Models;

namespace Hora.Core.Jira;

public interface IJiraClient
{
    /// <summary>GET /rest/api/3/myself – prüft die Zugangsdaten und liefert accountId, Anzeigename, Zeitzone.</summary>
    Task<JiraUser> GetMyselfAsync(CancellationToken cancellationToken);

    /// <summary>Alle Tickets, an denen der angemeldete Nutzer im Zeitraum (je einen Tag erweitert) Zeit gebucht hat.</summary>
    Task<IReadOnlyList<JiraIssue>> SearchIssuesWithWorklogsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>Alle Worklogs eines Tickets, die zwischen den beiden Zeitpunkten gestartet wurden (alle Autoren).</summary>
    Task<IReadOnlyList<JiraWorklog>> GetWorklogsAsync(string issueKey, DateTimeOffset startedAfter, DateTimeOffset startedBefore, CancellationToken cancellationToken);
}
