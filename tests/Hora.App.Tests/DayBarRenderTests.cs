using System.Windows;
using System.Windows.Controls;
using Hora.App.Controls;
using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.App.Views;
using Hora.Core.Jira.Models;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

/// <summary>
/// Baut Wochen-, Monats- und Tagesansicht wirklich auf und sieht nach, ob die drei Abschnitte des
/// Balkens dort ankommen. Die Rechnung ist anderswo geprüft; hier geht es um die Bindungen der
/// XAML-Dateien, die kein Übersetzungslauf bemängelt.
/// </summary>
public class DayBarRenderTests
{
    private const double ViewWidth = 900;
    private static readonly DateOnly Today = new(2026, 9, 16); // Mittwoch, Soll 8h
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    private static WorklogEntry Entry(int day, double hours) =>
        WorklogEntry.Jira("SYN-1", "Titel", new DateTimeOffset(2026, 9, day, 8, 0, 0, TimeSpan.FromHours(2)), TimeSpan.FromHours(hours), string.Empty, "https://example.atlassian.net");

    [StaFact]
    public void The_week_row_of_an_eleven_hour_day_shows_green_orange_and_red()
    {
        StaRunner.Run(() =>
        {
            AppResources.Ensure();

            var model = new WeekViewModel(WorkSchedule.Default());
            model.Show(WeekOf(Entry(16, 11)), Array.Empty<IssueTotal>(), Today);

            // Fünf Arbeitstage, fünf Balken; nur der Mittwoch trägt die elf Stunden.
            var bar = BarsOf(new WeekView { DataContext = model })[2];

            Assert.True(bar.TargetWidth.Value > 0, "Der grüne Abschnitt fehlt.");
            Assert.True(bar.OvertimeWidth.Value > 0, "Der orange Abschnitt fehlt.");
            Assert.True(bar.OverLimitWidth.Value > 0, "Der rote Abschnitt fehlt.");
        });
    }

    [StaFact]
    public void The_month_cell_of_an_eleven_hour_day_shows_green_orange_and_red()
    {
        StaRunner.Run(() =>
        {
            AppResources.Ensure();

            var model = new MonthViewModel(WorkSchedule.Default());
            model.Show(DaysOf(Entry(16, 11)), Today, Today, Array.Empty<IssueTotal>());

            var bars = BarsOf(new MonthView { DataContext = model })
                .Where(bar => bar.OverLimitWidth.Value > 0)
                .ToList();

            var bar = Assert.Single(bars); // genau der 16. September
            Assert.True(bar.TargetWidth.Value > 0, "Der grüne Abschnitt fehlt.");
            Assert.True(bar.OvertimeWidth.Value > 0, "Der orange Abschnitt fehlt.");
        });
    }

    [StaFact]
    public void The_stats_card_of_an_eleven_hour_day_shows_green_orange_and_red()
    {
        StaRunner.Run(() =>
        {
            AppResources.Ensure();

            var service = new FakeWorklogService
            {
                Respond = (_, _) => new WorklogFetchResult(new[] { Entry(16, 11) }, Array.Empty<string>(), Array.Empty<string>()),
            };
            var model = new OverviewViewModel(
                service,
                new JiraUser { AccountId = "me", DisplayName = "Simon" },
                Berlin,
                new NullLogger(),
                () => Today);
            model.LoadAsync().GetAwaiter().GetResult();

            // Die Kennzahlenkarte steht in der Übersicht über den drei Ansichten; ihr Balken ist der
            // erste des Baums.
            var bar = BarsOf(new OverviewView { DataContext = model }).First();

            Assert.True(bar.TargetWidth.Value > 0, "Der grüne Abschnitt fehlt.");
            Assert.True(bar.OvertimeWidth.Value > 0, "Der orange Abschnitt fehlt.");
            Assert.True(bar.OverLimitWidth.Value > 0, "Der rote Abschnitt fehlt.");
        });
    }

    private static WeekSummary WeekOf(params WorklogEntry[] entries) =>
        WorklogAggregator.BuildWeekSummary(entries, Today, Berlin);

    /// <summary>Das sichtbare Rechteck des Septembers 2026: 31.08. bis 04.10., fünf volle Wochen.</summary>
    private static IReadOnlyList<DaySummary> DaysOf(params WorklogEntry[] entries) =>
        Enumerable.Range(0, 35)
            .Select(offset => WorklogAggregator.BuildDaySummary(entries, new DateOnly(2026, 8, 31).AddDays(offset), Berlin))
            .ToList();

    private static IReadOnlyList<SegmentedRatioBar> BarsOf(FrameworkElement view)
    {
        // Wie in ProfileViewRenderTests: Ohne umgebendes Element baut WPF keinen visuellen Baum auf.
        var host = new Border { Child = view, Width = ViewWidth, Height = 600 };
        host.Measure(new Size(ViewWidth, 600));
        host.Arrange(new Rect(0, 0, ViewWidth, 600));
        host.UpdateLayout();

        return AppResources.Descendants(view).OfType<SegmentedRatioBar>().ToList();
    }
}
