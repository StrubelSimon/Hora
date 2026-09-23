using Hora.Core.Jira;
using Hora.Core.Jira.Models;

namespace Hora.App.Tests.TestDoubles;

internal sealed class FakeJiraClient : IJiraClient
{
    public Exception? MyselfFailure { get; set; }

    public JiraUser User { get; set; } = new() { AccountId = "me", DisplayName = "Simon", TimeZone = "Europe/Berlin" };

    public Task<JiraUser> GetMyselfAsync(CancellationToken cancellationToken) =>
        MyselfFailure is null ? Task.FromResult(User) : Task.FromException<JiraUser>(MyselfFailure);

    /// <summary>Wie oft Buchungen geholt wurden. Hält fest, ob ein Abruf zu viel oder zu wenig lief.</summary>
    public int SearchCalls { get; private set; }

    public Task<IReadOnlyList<JiraIssue>> SearchIssuesWithWorklogsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        SearchCalls++;
        return Task.FromResult<IReadOnlyList<JiraIssue>>(Array.Empty<JiraIssue>());
    }

    public Task<IReadOnlyList<JiraWorklog>> GetWorklogsAsync(string issueKey, DateTimeOffset startedAfter, DateTimeOffset startedBefore, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<JiraWorklog>>(Array.Empty<JiraWorklog>());
}
