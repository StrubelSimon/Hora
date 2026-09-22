using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.Core.Jira;
using Hora.Core.Jira.Models;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

/// <summary>
/// Das Zeitformat wirkt auf jede Dauer der Übersicht: Einträge, Tages- und Wochensummen,
/// Zwischensummen pro Ticket, Monatszellen und Kennzahlen. Außerdem: Der Zeitpunkt des letzten
/// erfolgreichen Abrufs wird nur nach einem erfolgreichen Abruf gemeldet.
/// </summary>
public class OverviewDurationFormatTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    private static readonly DateOnly Today = new(2026, 9, 16); // Mittwoch, KW 38

    private const string BaseUrl = "https://example.atlassian.net";

    private static WorklogEntry Entry(int day, int hour, int durationMinutes, string itemKey = "ABC-1") =>
        WorklogEntry.Jira(itemKey, "Titel", new DateTimeOffset(2026, 9, day, hour, 0, 0, TimeSpan.FromHours(2)),
            TimeSpan.FromMinutes(durationMinutes), string.Empty, BaseUrl);

    private static OverviewViewModel CreateViewModel(
        FakeWorklogService service,
        ViewMode initialMode = ViewMode.Day,
        DurationFormat durationFormat = DurationFormat.HoursMinutes) =>
        new(service, new JiraUser { AccountId = "me", DisplayName = "Simon" },
            Berlin, new NullLogger(), () => Today, WorkSchedule.Default(), initialMode, durationFormat);

    private static FakeWorklogService ServiceWithEntries() => new()
    {
        Respond = (_, _) => new WorklogFetchResult(
            new[]
            {
                Entry(16, 9, 465),              // Mi 16.09., 7h 45m
                Entry(16, 18, 15, "ABC-2"),     // Mi 16.09., 15m
                Entry(17, 9, 120),              // Do 17.09., 2h
            },
            Array.Empty<string>(),
            Array.Empty<string>()),
    };

    [Fact]
    public async Task Every_duration_of_the_day_view_follows_the_decimal_format()
    {
        var vm = CreateViewModel(ServiceWithEntries(), durationFormat: DurationFormat.DecimalHours);

        await vm.LoadAsync();

        Assert.Equal("7,75h", vm.Day.Rows[0].DurationText);      // einzelner Eintrag
        Assert.Equal("0,25h", vm.Day.Rows[1].DurationText);
        Assert.Equal("8h", vm.Day.TotalText);                    // Tagessumme
        Assert.Equal("7,75h", vm.Day.IssueTotals[0].TotalText);  // Zwischensumme pro Ticket
    }

    [Fact]
    public async Task Every_duration_of_the_week_view_follows_the_decimal_format()
    {
        var vm = CreateViewModel(ServiceWithEntries(), ViewMode.Week, DurationFormat.DecimalHours);

        await vm.LoadAsync();

        Assert.Equal("10h", vm.Week.TotalText);                  // Wochensumme
        Assert.Equal("8h", vm.Week.Days[2].TotalText);           // Tagessumme in der Wochenzeile
        Assert.Equal("2h", vm.Week.Days[3].TotalText);
        Assert.Equal("–", vm.Week.Days[4].TotalText);            // leerer Tag bleibt der Gedankenstrich (Fr 18.09.)
        Assert.Equal("9,75h", vm.Week.IssueTotals[0].TotalText); // ABC-1 über die Woche: 7h 45m + 2h
    }

    [Fact]
    public async Task Every_duration_of_the_month_view_follows_the_decimal_format()
    {
        var vm = CreateViewModel(ServiceWithEntries(), ViewMode.Month, DurationFormat.DecimalHours);

        await vm.LoadAsync();

        var weekOfToday = vm.Month.Weeks.Single(week => week.Days.Any(day => day.Date == Today));
        Assert.Equal("10h", weekOfToday.WeekTotalText);                                   // Wochensumme im Gitter
        Assert.Equal("8h", weekOfToday.Days.Single(day => day.Date == Today).TotalText);  // Tageszelle
        Assert.Equal("9,75h", vm.Month.IssueTotals[0].TotalText);
    }

    [Fact]
    public async Task The_stats_card_follows_the_decimal_format()
    {
        var vm = CreateViewModel(ServiceWithEntries(), durationFormat: DurationFormat.DecimalHours);

        await vm.LoadAsync();

        Assert.Equal("8h", vm.Stats.LoggedText);
        Assert.Equal("8h", vm.Stats.TargetText);
        Assert.Equal("0h", vm.Stats.DifferenceText);
    }

    [Fact]
    public async Task The_stats_card_shows_a_decimal_difference_with_a_real_minus_sign()
    {
        var service = new FakeWorklogService
        {
            Respond = (_, _) => new WorklogFetchResult(new[] { Entry(16, 9, 465) }, Array.Empty<string>(), Array.Empty<string>()),
        };
        var vm = CreateViewModel(service, durationFormat: DurationFormat.DecimalHours);

        await vm.LoadAsync();

        Assert.Equal("7,75h", vm.Stats.LoggedText);
        Assert.Equal("−0,25h", vm.Stats.DifferenceText);
    }

    [Fact]
    public async Task Switching_the_format_at_runtime_redraws_every_duration()
    {
        var vm = CreateViewModel(ServiceWithEntries());
        await vm.LoadAsync();

        Assert.Equal("7h 45m", vm.Day.Rows[0].DurationText);
        Assert.Equal("8h", vm.Day.TotalText);

        vm.DurationFormat = DurationFormat.DecimalHours;

        Assert.Equal("7,75h", vm.Day.Rows[0].DurationText);
        Assert.Equal("7,75h", vm.Day.IssueTotals[0].TotalText);
        Assert.Equal("8h", vm.Stats.LoggedText);

        vm.DurationFormat = DurationFormat.HoursMinutes;

        Assert.Equal("7h 45m", vm.Day.Rows[0].DurationText);
    }

    [Fact]
    public async Task Switching_the_format_without_any_loaded_data_does_not_throw()
    {
        var vm = CreateViewModel(new FakeWorklogService());

        vm.DurationFormat = DurationFormat.DecimalHours;

        Assert.Equal("0h", vm.Day.TotalText);

        await vm.LoadAsync();

        Assert.Equal("0h", vm.Day.TotalText);
    }

    [Fact]
    public void The_initial_mode_is_taken_over_together_with_its_period_label()
    {
        var vm = CreateViewModel(new FakeWorklogService(), ViewMode.Month);

        Assert.True(vm.IsMonthMode);
        Assert.Equal("September 2026", vm.PeriodLabel);
    }

    [Fact]
    public async Task A_month_start_loads_the_visible_rectangle_right_away()
    {
        var service = new FakeWorklogService();
        var vm = CreateViewModel(service, ViewMode.Month);

        await vm.LoadAsync();

        var call = Assert.Single(service.Calls);
        Assert.Equal((new DateOnly(2026, 8, 31), new DateOnly(2026, 10, 4)), call);
    }

    [Fact]
    public async Task A_successful_fetch_reports_a_synchronisation()
    {
        var vm = CreateViewModel(new FakeWorklogService());
        var reports = 0;
        vm.SyncSucceeded += () => reports++;

        await vm.LoadAsync();

        Assert.Equal(1, reports);
    }

    [Fact]
    public async Task A_failed_fetch_reports_no_synchronisation()
    {
        var vm = CreateViewModel(new FakeWorklogService { ThrowOnce = new JiraUnavailableException("offline") });
        var reports = 0;
        vm.SyncSucceeded += () => reports++;

        await vm.LoadAsync();

        Assert.Equal(0, reports);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public async Task A_fetch_answered_from_the_cache_reports_no_new_synchronisation()
    {
        var vm = CreateViewModel(new FakeWorklogService());
        await vm.LoadAsync();
        var reports = 0;
        vm.SyncSucceeded += () => reports++;

        await vm.LoadAsync(); // derselbe Bereich: aus dem Zwischenspeicher, kein neuer Abruf

        Assert.Equal(0, reports);
    }

    [Fact]
    public void The_avatar_asks_for_the_profile_page()
    {
        var vm = CreateViewModel(new FakeWorklogService());
        var requests = 0;
        vm.ProfileRequested += () => requests++;

        vm.ShowProfileCommand.Execute(null);

        Assert.Equal(1, requests);
    }
}
