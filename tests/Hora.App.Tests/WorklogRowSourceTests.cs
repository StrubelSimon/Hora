using Hora.App.ViewModels;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

public class WorklogRowSourceTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    [Fact]
    public void JiraEntryShowsTimeRange()
    {
        var entry = WorklogEntry.Jira(
            "PROJ-123",
            "Angebot schreiben",
            new DateTimeOffset(2026, 9, 21, 9, 0, 0, TimeSpan.FromHours(2)),
            TimeSpan.FromMinutes(90),
            string.Empty,
            "https://advisori-ftc.atlassian.net");

        var row = WorklogRow.From(entry, Berlin, DurationFormat.HoursMinutes);

        Assert.Equal(WorklogSource.Jira, row.Source);
        Assert.Equal("PROJ-123", row.ItemKey);
        Assert.Equal("09:00 – 10:30", row.TimeRangeText);
        Assert.True(row.HasStartTime);
        Assert.Equal("https://advisori-ftc.atlassian.net/browse/PROJ-123", row.ItemUrl);
    }

    /// <summary>
    /// Asana kennt nur einen Tag, keine Uhrzeit. Eine erfundene Zeitspanne wäre eine Behauptung
    /// über etwas, das Asana nie gespeichert hat — die Zeile zeigt deshalb allein die Dauer.
    /// </summary>
    [Fact]
    public void AsanaEntryShowsDurationInsteadOfTimeRange()
    {
        var entry = new WorklogEntry(
            WorklogSource.Asana,
            "Marketing",
            "Entwicklung",
            new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2)),
            TimeSpan.FromMinutes(90),
            string.Empty,
            "https://app.asana.com/0/700",
            HasStartTime: false);

        var row = WorklogRow.From(entry, Berlin, DurationFormat.HoursMinutes);

        Assert.Equal(WorklogSource.Asana, row.Source);
        Assert.False(row.HasStartTime);
        Assert.Equal("1h 30m", row.TimeRangeText);
        Assert.Equal(string.Empty, row.StartText);
    }

    /// <summary>
    /// Jira-Ticket und Asana-Projekt könnten denselben Text tragen. Gruppiert würde nach dem Text
    /// allein, flössen ihre Zeiten in eine Zeile zusammen — die Quelle gehört in den Schlüssel.
    /// </summary>
    [Fact]
    public void IssueTotalsKeepSourcesApartEvenWhenTheKeyMatches()
    {
        var entries = new[]
        {
            WorklogEntry.Jira("ABC", "Jira-Ticket", new DateTimeOffset(2026, 9, 21, 9, 0, 0, TimeSpan.FromHours(2)),
                TimeSpan.FromHours(1), string.Empty, "https://example.atlassian.net"),
            new WorklogEntry(WorklogSource.Asana, "ABC", "Asana-Projekt",
                new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2)),
                TimeSpan.FromHours(2), string.Empty, "https://app.asana.com/0/1", HasStartTime: false),
        };

        var totals = WorklogAggregator.BuildIssueTotals(entries);

        Assert.Equal(2, totals.Count);
        Assert.Contains(totals, total => total.Source == WorklogSource.Jira && total.Total == TimeSpan.FromHours(1));
        Assert.Contains(totals, total => total.Source == WorklogSource.Asana && total.Total == TimeSpan.FromHours(2));
    }
}
