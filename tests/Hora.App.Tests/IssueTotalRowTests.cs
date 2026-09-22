using Hora.App.ViewModels;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

/// <summary>
/// Balken im Abschnitt „Pro Ticket“ (Abschnitt 3.7 der Gestaltungsvorgaben): Jede Zeile trägt eine
/// Verhältniszahl zwischen 0 und 1, bemessen am größten Eintrag derselben Menge.
/// </summary>
public class IssueTotalRowTests
{
    private const string BaseUrl = "https://example.atlassian.net";

    private static IssueTotal Total(string issueKey, TimeSpan duration) =>
        new(issueKey, $"Titel {issueKey}", duration, 1);

    [Fact]
    public void Largest_entry_fills_the_bar_completely()
    {
        var totals = new[]
        {
            Total("ABC-1", TimeSpan.FromHours(4)),
            Total("ABC-2", TimeSpan.FromHours(1)),
        };
        var maxTotal = IssueTotalRow.MaxTotalOf(totals);

        var row = IssueTotalRow.From(totals[0], BaseUrl, maxTotal);

        Assert.Equal(1.0, row.BarRatio, 6);
    }

    [Fact]
    public void Half_as_large_an_entry_fills_the_bar_halfway()
    {
        var totals = new[]
        {
            Total("ABC-1", TimeSpan.FromHours(4)),
            Total("ABC-2", TimeSpan.FromHours(2)),
        };
        var maxTotal = IssueTotalRow.MaxTotalOf(totals);

        var row = IssueTotalRow.From(totals[1], BaseUrl, maxTotal);

        Assert.Equal(0.5, row.BarRatio, 6);
    }

    [Fact]
    public void An_empty_set_yields_a_zero_maximum_and_does_not_throw()
    {
        var maxTotal = IssueTotalRow.MaxTotalOf(Array.Empty<IssueTotal>());

        Assert.Equal(TimeSpan.Zero, maxTotal);
    }

    [Fact]
    public void A_zero_maximum_yields_a_ratio_of_zero_instead_of_dividing_by_zero()
    {
        var totals = new[]
        {
            Total("ABC-1", TimeSpan.Zero),
            Total("ABC-2", TimeSpan.Zero),
        };
        var maxTotal = IssueTotalRow.MaxTotalOf(totals);

        var rows = totals.Select(total => IssueTotalRow.From(total, BaseUrl, maxTotal)).ToList();

        Assert.Equal(TimeSpan.Zero, maxTotal);
        Assert.All(rows, row => Assert.Equal(0.0, row.BarRatio, 6));
    }

    [Fact]
    public void The_ratio_never_leaves_the_range_from_zero_to_one()
    {
        var totals = new[]
        {
            Total("ABC-1", TimeSpan.FromHours(4)),
            Total("ABC-2", TimeSpan.FromMinutes(45)),
            Total("ABC-3", TimeSpan.FromMinutes(15)),
        };
        var maxTotal = IssueTotalRow.MaxTotalOf(totals);

        var rows = totals.Select(total => IssueTotalRow.From(total, BaseUrl, maxTotal)).ToList();

        Assert.All(rows, row => Assert.InRange(row.BarRatio, 0.0, 1.0));
        Assert.Equal(0.1875, rows[1].BarRatio, 6);
        Assert.Equal(0.0625, rows[2].BarRatio, 6);
    }

    [Fact]
    public void Showing_a_day_without_issue_totals_does_not_throw()
    {
        var viewModel = new DayViewModel(TimeZoneInfo.Utc, BaseUrl);
        var day = new DaySummary(new DateOnly(2026, 9, 17), Array.Empty<WorklogEntry>(), TimeSpan.Zero);

        viewModel.Show(day, Array.Empty<IssueTotal>());

        Assert.Empty(viewModel.IssueTotals);
    }
}
