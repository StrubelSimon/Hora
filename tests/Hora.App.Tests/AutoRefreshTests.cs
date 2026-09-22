using Hora.App.Services;
using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.Core.Jira;
using Hora.Core.Jira.Models;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

/// <summary>
/// Die Übersicht holt die Buchungen in einem einstellbaren Abstand von selbst nach. Der Abruf läuft
/// still: Er lässt den angezeigten Stand stehen, bis die neuen Daten da sind, und meldet einen
/// Fehlschlag nicht auf dem Schirm — sonst stünde alle paar Minuten eine rote Zeile über Zahlen,
/// die in Ordnung sind.
/// </summary>
public class AutoRefreshTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    private static readonly DateOnly Today = new(2026, 9, 16); // Mittwoch, KW 38

    private const string BaseUrl = "https://example.atlassian.net";

    private static WorklogEntry Entry(int day, int hour, int durationMinutes, string itemKey = "ABC-1") =>
        WorklogEntry.Jira(itemKey, "Titel", new DateTimeOffset(2026, 9, day, hour, 0, 0, TimeSpan.FromHours(2)),
            TimeSpan.FromMinutes(durationMinutes), string.Empty, BaseUrl);

    private static FakeWorklogService ServiceWithOneEntry() => new()
    {
        Respond = (_, _) => new WorklogFetchResult(new[] { Entry(16, 9, 60) }, Array.Empty<string>(), Array.Empty<string>()),
    };

    private static OverviewViewModel CreateViewModel(FakeWorklogService service, IRefreshScheduler scheduler) =>
        new(service, new JiraUser { AccountId = "me", DisplayName = "Simon" },
            Berlin, new NullLogger(), () => Today, refreshScheduler: scheduler);

    // ===================== Takt an und aus =====================

    [Fact]
    public void Setting_an_interval_starts_the_scheduler()
    {
        var scheduler = new FakeRefreshScheduler();
        var vm = CreateViewModel(new FakeWorklogService(), scheduler);

        vm.AutoRefreshMinutes = 5;

        Assert.True(scheduler.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(5), scheduler.Interval);
    }

    [Fact]
    public void Setting_the_interval_to_zero_stops_the_scheduler()
    {
        var scheduler = new FakeRefreshScheduler();
        var vm = CreateViewModel(new FakeWorklogService(), scheduler);
        vm.AutoRefreshMinutes = 5;

        vm.AutoRefreshMinutes = 0;

        Assert.False(scheduler.IsRunning);
    }

    [Fact]
    public async Task A_tick_fetches_the_entries_again()
    {
        var service = ServiceWithOneEntry();
        var scheduler = new FakeRefreshScheduler();
        var vm = CreateViewModel(service, scheduler);
        await vm.LoadAsync();
        vm.AutoRefreshMinutes = 5;

        await scheduler.TickAsync();

        Assert.Equal(2, service.Calls.Count);
    }

    [Fact]
    public async Task A_successful_load_lets_the_interval_begin_anew()
    {
        // Sonst käme unmittelbar nach einem Abruf von Hand noch der Takt hinterher.
        var scheduler = new FakeRefreshScheduler();
        var vm = CreateViewModel(ServiceWithOneEntry(), scheduler);
        vm.AutoRefreshMinutes = 5;
        var startsBefore = scheduler.StartCalls;

        await vm.LoadAsync();

        Assert.Equal(startsBefore + 1, scheduler.StartCalls);
        Assert.True(scheduler.IsRunning);
    }

    // ===================== Der stille Abruf =====================

    [Fact]
    public async Task A_running_silent_refresh_leaves_the_shown_entries_in_place()
    {
        var service = ServiceWithOneEntry();
        var scheduler = new FakeRefreshScheduler();
        var vm = CreateViewModel(service, scheduler);
        await vm.LoadAsync();
        vm.AutoRefreshMinutes = 5;

        var gate = new TaskCompletionSource();
        service.Gate = gate;
        var tick = scheduler.TickAsync();

        // Während der Abruf läuft: derselbe Stand wie vorher, kein Ladezustand.
        Assert.Single(vm.Day.Rows);
        Assert.False(vm.IsLoading);

        gate.SetResult();
        await tick;
        Assert.Single(vm.Day.Rows);
    }

    [Fact]
    public async Task A_failed_silent_refresh_shows_no_error_message()
    {
        var service = ServiceWithOneEntry();
        var scheduler = new FakeRefreshScheduler();
        var vm = CreateViewModel(service, scheduler);
        await vm.LoadAsync();
        vm.AutoRefreshMinutes = 5;

        service.ThrowOnce = new JiraException("Netz weg");
        await scheduler.TickAsync();

        Assert.Null(vm.ErrorMessage);
        Assert.Single(vm.Day.Rows); // Der zuletzt geglückte Stand bleibt stehen.
    }

    [Fact]
    public async Task A_silent_refresh_still_reports_an_expired_session()
    {
        var service = ServiceWithOneEntry();
        var scheduler = new FakeRefreshScheduler();
        var vm = CreateViewModel(service, scheduler);
        await vm.LoadAsync();
        vm.AutoRefreshMinutes = 5;
        var expired = false;
        vm.SessionExpired += () => expired = true;

        service.ThrowOnce = new JiraAuthenticationException();
        await scheduler.TickAsync();

        Assert.True(expired);
    }

    [Fact]
    public async Task A_tick_during_a_running_load_is_skipped()
    {
        var service = ServiceWithOneEntry();
        var scheduler = new FakeRefreshScheduler();
        var vm = CreateViewModel(service, scheduler);
        vm.AutoRefreshMinutes = 5;

        var gate = new TaskCompletionSource();
        service.Gate = gate;
        var running = vm.LoadAsync();

        await scheduler.TickAsync();
        Assert.Single(service.Calls);

        gate.SetResult();
        await running;
    }
}
