namespace Hora.Core.Jira.Models;

/// <summary>Antwort von GET /rest/api/3/search/jql.</summary>
public sealed class SearchResponse
{
    public List<JiraIssue> Issues { get; set; } = new();

    public string? NextPageToken { get; set; }

    public bool? IsLast { get; set; }
}
