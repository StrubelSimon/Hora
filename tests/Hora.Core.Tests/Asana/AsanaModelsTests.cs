using System.Text.Json;
using Hora.Core.Asana;
using Hora.Core.Asana.Models;

namespace Hora.Core.Tests.Asana;

public class AsanaModelsTests
{
    [Fact]
    public void ReadsUserWithWorkspaces()
    {
        const string json = """
        {"data":{"gid":"111","name":"Simon Strubel","email":"simon.strubel@advisori.de",
        "workspaces":[{"gid":"900","name":"ADVISORI"},{"gid":"901","name":"Privat"}]}}
        """;

        var response = JsonSerializer.Deserialize<AsanaResponse<AsanaUser>>(json, AsanaJson.Options)!;

        Assert.Equal("111", response.Data!.Gid);
        Assert.Equal("Simon Strubel", response.Data.Name);
        Assert.Equal("simon.strubel@advisori.de", response.Data.Email);
        Assert.Equal(2, response.Data.Workspaces.Count);
        Assert.Equal("ADVISORI", response.Data.Workspaces[0].Name);
    }

    [Fact]
    public void ReadsTimeEntryWithProjectAndPagination()
    {
        const string json = """
        {"data":[{"gid":"5","duration_minutes":90,"entered_on":"2026-09-21",
        "attributable_to":{"gid":"700","name":"Marketing"},
        "created_by":{"gid":"111","name":"Simon Strubel"},
        "categories":[{"gid":"800","name":"Entwicklung"}]}],
        "next_page":{"offset":"eyJ0","path":"/time_tracking_entries?offset=eyJ0","uri":"https://app.asana.com/x"}}
        """;

        var response = JsonSerializer.Deserialize<AsanaResponse<List<AsanaTimeEntry>>>(json, AsanaJson.Options)!;

        var entry = Assert.Single(response.Data!);
        Assert.Equal(90, entry.DurationMinutes);
        Assert.Equal(new DateOnly(2026, 9, 21), entry.EnteredOn);
        Assert.Equal("Marketing", entry.AttributableTo!.Name);
        Assert.Equal("111", entry.CreatedBy!.Gid);
        Assert.Equal("Entwicklung", Assert.Single(entry.Categories).Name);
        Assert.Equal("eyJ0", response.NextPage!.Offset);
    }

    [Fact]
    public void LastPageHasNullNextPage()
    {
        const string json = """{"data":[],"next_page":null}""";

        var response = JsonSerializer.Deserialize<AsanaResponse<List<AsanaTimeEntry>>>(json, AsanaJson.Options)!;

        Assert.Null(response.NextPage);
        Assert.Empty(response.Data!);
    }

    /// <summary>
    /// Fall A aus Abschnitt 3.3 der Spezifikation: Kommt der Task mit, muss er gelesen werden.
    /// Fehlt er (Fall B), bleibt die Eigenschaft null — beides darf nicht werfen.
    /// </summary>
    [Fact]
    public void ReadsOptionalTaskWhenPresentAndToleratesItsAbsence()
    {
        const string withTask = """
        {"data":[{"gid":"5","duration_minutes":30,"entered_on":"2026-09-21",
        "task":{"gid":"600","name":"Angebot schreiben","permalink_url":"https://app.asana.com/0/0/600"}}]}
        """;
        const string withoutTask = """
        {"data":[{"gid":"6","duration_minutes":30,"entered_on":"2026-09-21"}]}
        """;

        var withTaskEntry = Assert.Single(JsonSerializer.Deserialize<AsanaResponse<List<AsanaTimeEntry>>>(withTask, AsanaJson.Options)!.Data!);
        var withoutTaskEntry = Assert.Single(JsonSerializer.Deserialize<AsanaResponse<List<AsanaTimeEntry>>>(withoutTask, AsanaJson.Options)!.Data!);

        Assert.Equal("Angebot schreiben", withTaskEntry.Task!.Name);
        Assert.Equal("https://app.asana.com/0/0/600", withTaskEntry.Task.PermalinkUrl);
        Assert.Null(withoutTaskEntry.Task);
        Assert.Null(withoutTaskEntry.AttributableTo);
        Assert.Empty(withoutTaskEntry.Categories);
    }
}
