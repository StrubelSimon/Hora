namespace Hora.Core.Jira.Models;

/// <summary>Antwort von GET /rest/api/3/myself; auch als Autor eines Worklogs.</summary>
public sealed class JiraUser
{
    public string AccountId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? EmailAddress { get; set; }

    public string? TimeZone { get; set; }
}
