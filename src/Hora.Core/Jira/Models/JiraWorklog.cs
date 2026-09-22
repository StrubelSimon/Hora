using System.Text.Json;

namespace Hora.Core.Jira.Models;

public sealed class JiraWorklog
{
    public string Id { get; set; } = string.Empty;

    public string IssueId { get; set; } = string.Empty;

    public JiraUser? Author { get; set; }

    /// <summary>Roher Jira-Zeitstempel, z. B. "2021-01-17T12:34:00.000+0000". Parsen mit JiraDateParser.</summary>
    public string Started { get; set; } = string.Empty;

    public long TimeSpentSeconds { get; set; }

    /// <summary>ADF-Dokument oder null. Klartext über AdfTextExtractor.</summary>
    public JsonElement? Comment { get; set; }
}
