using Hora.App.ViewModels;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

/// <summary>
/// Die Tagesansicht führt zwei Zeilenlisten: die chronologischen Jira-Buchungen und darunter, in
/// einem eigenen Block, die Asana-Einträge. Sie tragen keine Uhrzeit und täuschten in der
/// chronologischen Liste eine Ordnung vor, die es nicht gibt.
/// </summary>
public class DayViewAsanaBlockTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    private static WorklogEntry Jira(string key, int hour) => WorklogEntry.Jira(
        key, key, new DateTimeOffset(2026, 9, 21, hour, 0, 0, TimeSpan.FromHours(2)),
        TimeSpan.FromHours(1), string.Empty, "https://example.atlassian.net");

    private static WorklogEntry Asana(string project) => new(
        WorklogSource.Asana, project, project,
        new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2)),
        TimeSpan.FromHours(2), string.Empty, "https://app.asana.com/0/1", HasStartTime: false);

    private static DayViewModel ShowDay(params WorklogEntry[] entries)
    {
        var viewModel = new DayViewModel(Berlin);
        var summary = WorklogAggregator.BuildDaySummary(entries, new DateOnly(2026, 9, 21), Berlin);
        viewModel.Show(summary, WorklogAggregator.BuildIssueTotals(summary.Entries));
        return viewModel;
    }

    [Fact]
    public void JiraAndAsanaEntriesLandInSeparateLists()
    {
        var viewModel = ShowDay(Jira("A-1", 9), Asana("Marketing"), Jira("A-2", 14));

        Assert.Equal(new[] { "A-1", "A-2" }, viewModel.Rows.Select(row => row.ItemKey));
        Assert.Equal("Marketing", Assert.Single(viewModel.AsanaRows).ItemKey);
        Assert.True(viewModel.HasAsanaRows);
    }

    /// <summary>Ohne Asana-Zeiten bleibt der Block samt Zwischenüberschrift ausgeblendet.</summary>
    [Fact]
    public void WithoutAsanaEntriesTheBlockStaysHidden()
    {
        var viewModel = ShowDay(Jira("A-1", 9));

        Assert.False(viewModel.HasAsanaRows);
        Assert.Empty(viewModel.AsanaRows);
    }

    /// <summary>Die Tagessumme umfasst weiterhin beide Quellen — getrennt wird nur die Darstellung.</summary>
    [Fact]
    public void TotalCoversBothSources()
    {
        var viewModel = ShowDay(Jira("A-1", 9), Asana("Marketing"));

        // Eine Jira-Stunde plus zwei Asana-Stunden; glatte Stunden schreibt DurationFormatter ohne
        // Minutenteil. Ginge eine der beiden Quellen verloren, stünde hier „1h“ beziehungsweise „2h“.
        Assert.Equal("3h", viewModel.TotalText);
        Assert.False(viewModel.IsEmpty);
    }

    /// <summary>
    /// Ein Tag mit ausschließlich Asana-Zeiten ist nicht leer: Stünde nur die Jira-Liste für
    /// „leer“, bliebe der Hinweis „Keine Buchungen an diesem Tag“ über dem gefüllten Asana-Block
    /// stehen und der Abschnitt „Pro Ticket“ verschwände.
    /// </summary>
    [Fact]
    public void ADayWithOnlyAsanaEntriesIsNotEmpty()
    {
        var viewModel = ShowDay(Asana("Marketing"));

        Assert.Empty(viewModel.Rows);
        Assert.True(viewModel.HasAsanaRows);
        Assert.False(viewModel.IsEmpty);
    }

    /// <summary>Ein zweiter Aufruf zeigt den neuen Tag und nicht die Einträge des alten obendrauf.</summary>
    [Fact]
    public void ShowingAnotherDayReplacesBothLists()
    {
        var viewModel = new DayViewModel(Berlin);

        var withAsana = WorklogAggregator.BuildDaySummary(
            new[] { Jira("A-1", 9), Asana("Marketing") }, new DateOnly(2026, 9, 21), Berlin);
        viewModel.Show(withAsana, WorklogAggregator.BuildIssueTotals(withAsana.Entries));

        var withoutAsana = WorklogAggregator.BuildDaySummary(
            new[] { Jira("A-2", 10) }, new DateOnly(2026, 9, 21), Berlin);
        viewModel.Show(withoutAsana, WorklogAggregator.BuildIssueTotals(withoutAsana.Entries));

        Assert.Equal("A-2", Assert.Single(viewModel.Rows).ItemKey);
        Assert.Empty(viewModel.AsanaRows);
        Assert.False(viewModel.HasAsanaRows);
    }
}
