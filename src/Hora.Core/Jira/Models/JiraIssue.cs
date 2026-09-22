namespace Hora.Core.Jira.Models;

public sealed class JiraIssue
{
    public string Id { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public JiraIssueFields Fields { get; set; } = new();
}

public sealed class JiraIssueFields
{
    public string Summary { get; set; } = string.Empty;
}
