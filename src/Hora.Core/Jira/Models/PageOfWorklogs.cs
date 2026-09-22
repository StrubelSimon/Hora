namespace Hora.Core.Jira.Models;

/// <summary>Antwort von GET /rest/api/3/issue/{key}/worklog.</summary>
public sealed class PageOfWorklogs
{
    public int StartAt { get; set; }

    public int MaxResults { get; set; }

    public int Total { get; set; }

    public List<JiraWorklog> Worklogs { get; set; } = new();
}
