using System.Text.Json;
using Hora.Core.Jira;
using Hora.Core.Jira.Models;

namespace Hora.Core.Tests.Jira;

public class JiraModelsTests
{
    [Fact]
    public void PageOfWorklogs_deserializes_the_official_sample()
    {
        const string json = """
            { "maxResults": 1, "startAt": 0, "total": 1, "worklogs": [ {
                "author": { "accountId": "5b10a2844c20165700ede21g", "displayName": "Mia Krystof" },
                "comment": { "type": "doc", "version": 1, "content": [ { "type": "paragraph", "content": [ { "type": "text", "text": "I did some work here." } ] } ] },
                "id": "100028", "issueId": "10002",
                "started": "2021-01-17T12:34:00.000+0000",
                "timeSpent": "3h 20m", "timeSpentSeconds": 12000 } ] }
            """;

        var page = JsonSerializer.Deserialize<PageOfWorklogs>(json, JiraJson.Options)!;

        Assert.Equal(1, page.Total);
        var worklog = Assert.Single(page.Worklogs);
        Assert.Equal("5b10a2844c20165700ede21g", worklog.Author!.AccountId);
        Assert.Equal("2021-01-17T12:34:00.000+0000", worklog.Started);
        Assert.Equal(12000, worklog.TimeSpentSeconds);
        Assert.Equal("I did some work here.", AdfTextExtractor.Extract(worklog.Comment));
    }

    [Fact]
    public void JiraWorklog_without_comment_has_null_comment()
    {
        var worklog = JsonSerializer.Deserialize<JiraWorklog>(
            """{"id":"1","issueId":"2","started":"2021-01-17T12:34:00.000+0000","timeSpentSeconds":60}""",
            JiraJson.Options)!;

        Assert.Null(worklog.Comment);
    }

    [Fact]
    public void SearchResponse_deserializes_issues_and_paging_fields()
    {
        var response = JsonSerializer.Deserialize<SearchResponse>(
            """{"issues":[{"id":"1","key":"ABC-1","fields":{"summary":"Erstes"}}],"nextPageToken":"t2","isLast":false}""",
            JiraJson.Options)!;

        var issue = Assert.Single(response.Issues);
        Assert.Equal("ABC-1", issue.Key);
        Assert.Equal("Erstes", issue.Fields.Summary);
        Assert.Equal("t2", response.NextPageToken);
        Assert.False(response.IsLast);
    }

    [Fact]
    public void JiraUser_deserializes_myself_response()
    {
        var user = JsonSerializer.Deserialize<JiraUser>(
            """{"accountId":"abc","displayName":"Simon","emailAddress":"s@x.de","timeZone":"Europe/Berlin"}""",
            JiraJson.Options)!;

        Assert.Equal("abc", user.AccountId);
        Assert.Equal("Simon", user.DisplayName);
        Assert.Equal("Europe/Berlin", user.TimeZone);
    }
}
