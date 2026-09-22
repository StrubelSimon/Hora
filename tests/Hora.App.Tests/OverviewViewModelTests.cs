using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.Core.Jira;
using Hora.Core.Jira.Models;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

public class OverviewViewModelTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    private static readonly DateOnly Today = new(2026, 9, 16); // Mittwoch, KW 38

    private const string BaseUrl = "https://example.atlassian.net";

    private static WorklogEntry Entry(int day, int hour, int durationMinutes, string itemKey = "ABC-1") =>
        WorklogEntry.Jira(itemKey, "Titel", new DateTimeOffset(2026, 9, day, hour, 0, 0, TimeSpan.FromHours(2)),
            TimeSpan.FromMinutes(durationMinutes), string.Empty, BaseUrl);

    private static OverviewViewModel CreateViewModel(FakeWorklogService service) =>
        new(service, new JiraUser { AccountId = "me", DisplayName = "Simon" },
            Berlin, new NullLogger(), () => Today);

    [Fact]
    public void Initial_state_is_today_in_day_mode()
    {
        var vm = CreateViewModel(new FakeWorklogService());

        Assert.Equal(Today, vm.SelectedDate);
        Assert.True(vm.IsDayMode);
        Assert.False(vm.IsWeekMode);
        Assert.Equal("16. September 2026", vm.PeriodLabel);
        Assert.Equal("Simon", vm.UserDisplayName);
        Assert.StartsWith("Hora ", vm.AppTitle);
    }

    [Fact]
    public async Task LoadAsync_requests_monday_to_sunday_of_the_selected_week()
    {
        var service = new FakeWorklogService();
        var vm = CreateViewModel(service);

        await vm.LoadAsync();

        var call = Assert.Single(service.Calls);
        Assert.Equal((new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20)), call);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task Previous_within_the_same_week_does_not_reload()
    {
        var service = new FakeWorklogService();
        var vm = CreateViewModel(service);
        await vm.LoadAsync();

        await vm.PreviousCommand.ExecuteAsync(null);

        Assert.Equal(new DateOnly(2026, 9, 15), vm.SelectedDate);
        Assert.Equal("15. September 2026", vm.PeriodLabel);
        Assert.Single(service.Calls);
    }

    [Fact]
    public async Task Previous_across_the_week_boundary_reloads_the_new_week()
    {
        var service = new FakeWorklogService();
        var vm = CreateViewModel(service);
        await vm.LoadAsync();

        await vm.PreviousCommand.ExecuteAsync(null); // 15.09.
        await vm.PreviousCommand.ExecuteAsync(null); // 14.09. (Montag)
        await vm.PreviousCommand.ExecuteAsync(null); // 13.09. (Sonntag der Vorwoche)

        Assert.Equal(2, service.Calls.Count);
        Assert.Equal((new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13)), service.Calls[1]);
    }

    [Fact]
    public async Task Next_moves_forward_one_day()
    {
        var vm = CreateViewModel(new FakeWorklogService());
        await vm.LoadAsync();

        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal(new DateOnly(2026, 9, 17), vm.SelectedDate);
    }

    [Fact]
    public async Task Week_mode_moves_by_seven_days_and_shows_week_label()
    {
        var service = new FakeWorklogService();
        var vm = CreateViewModel(service);
        await vm.LoadAsync();
        vm.ShowWeekCommand.Execute(null);

        Assert.True(vm.IsWeekMode);
        Assert.Equal("14. – 20. September 2026", vm.PeriodLabel);

        await vm.PreviousCommand.ExecuteAsync(null);

        Assert.Equal(new DateOnly(2026, 9, 9), vm.SelectedDate);
        Assert.Equal("7. – 13. September 2026", vm.PeriodLabel);
        Assert.Equal(2, service.Calls.Count);
    }

    [Fact]
    public async Task Switching_between_day_and_week_does_not_reload()
    {
        var service = new FakeWorklogService();
        var vm = CreateViewModel(service);
        await vm.LoadAsync();

        vm.ShowWeekCommand.Execute(null);
        vm.ShowDayCommand.Execute(null);

        Assert.Single(service.Calls);
    }

    [Fact]
    public async Task Refresh_reloads_the_current_week()
    {
        var service = new FakeWorklogService();
        var vm = CreateViewModel(service);
        await vm.LoadAsync();

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, service.Calls.Count);
    }

    [Fact]
    public async Task Today_returns_to_the_current_date()
    {
        var vm = CreateViewModel(new FakeWorklogService());
        await vm.LoadAsync();
        for (var i = 0; i < 10; i++)
        {
            await vm.PreviousCommand.ExecuteAsync(null);
        }

        await vm.TodayCommand.ExecuteAsync(null);

        Assert.Equal(Today, vm.SelectedDate);
    }

    [Fact]
    public async Task Day_and_week_totals_reflect_loaded_entries()
    {
        var service = new FakeWorklogService
        {
            Respond = (_, _) => new WorklogFetchResult(
                new[] { Entry(16, 9, 60), Entry(16, 14, 30, "ABC-2"), Entry(17, 10, 45) },
                Array.Empty<string>()),
        };
        var vm = CreateViewModel(service);

        await vm.LoadAsync();

        Assert.Equal("1h 30m", vm.Day.TotalText);
        Assert.Equal(2, vm.Day.Rows.Count);
        Assert.Equal("09:00", vm.Day.Rows[0].StartText);
        Assert.Equal("1h", vm.Day.Rows[0].DurationText);
        Assert.Equal("https://example.atlassian.net/browse/ABC-1", vm.Day.Rows[0].ItemUrl);
        Assert.False(vm.Day.IsEmpty);
        Assert.Equal(2, vm.Day.IssueTotals.Count);

        Assert.Equal("2h 15m", vm.Week.TotalText);
        // Die Woche zeigt nur die Arbeitstage: Montag bis Freitag, kein Samstag und kein Sonntag.
        Assert.Equal(5, vm.Week.Days.Count);
        Assert.True(vm.Week.Days[2].IsToday);
        Assert.Equal("Mi 16.09.", vm.Week.Days[2].Label);
        Assert.Equal("1h 30m", vm.Week.Days[2].TotalText);
        Assert.Equal("–", vm.Week.Days[4].TotalText); // Fr 18.09., ohne Buchung
        Assert.False(vm.Week.IsEmpty);
        Assert.Equal(2, vm.Week.IssueTotals.Count);
        Assert.Equal("ABC-1", vm.Week.IssueTotals[0].ItemKey);
    }

    [Fact]
    public async Task Empty_week_marks_day_and_week_as_empty()
    {
        var vm = CreateViewModel(new FakeWorklogService());

        await vm.LoadAsync();

        Assert.True(vm.Day.IsEmpty);
        Assert.True(vm.Week.IsEmpty);
        Assert.Equal("0m", vm.Day.TotalText);
        Assert.Equal("0m", vm.Week.TotalText);
    }

    [Fact]
    public async Task ShowDayFor_switches_to_day_mode_without_reloading()
    {
        var service = new FakeWorklogService();
        var vm = CreateViewModel(service);
        await vm.LoadAsync();
        vm.ShowWeekCommand.Execute(null);

        vm.ShowDayFor(new DateOnly(2026, 9, 18));

        Assert.True(vm.IsDayMode);
        Assert.Equal(new DateOnly(2026, 9, 18), vm.SelectedDate);
        Assert.Equal("18. September 2026", vm.PeriodLabel);
        Assert.Single(service.Calls);
    }

    [Fact]
    public async Task Selecting_a_day_in_the_week_view_switches_to_that_day()
    {
        var vm = CreateViewModel(new FakeWorklogService());
        await vm.LoadAsync();
        vm.ShowWeekCommand.Execute(null);

        vm.Week.SelectDayCommand.Execute(vm.Week.Days[4]);

        Assert.True(vm.IsDayMode);
        Assert.Equal(new DateOnly(2026, 9, 18), vm.SelectedDate);
    }

    [Fact]
    public async Task Authentication_failure_raises_SessionExpired()
    {
        var service = new FakeWorklogService { ThrowOnce = new JiraAuthenticationException() };
        var vm = CreateViewModel(service);
        var expired = false;
        vm.SessionExpired += () => expired = true;

        await vm.LoadAsync();

        Assert.True(expired);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task Rate_limit_sets_error_message()
    {
        var vm = CreateViewModel(new FakeWorklogService { ThrowOnce = new JiraRateLimitException() });

        await vm.LoadAsync();

        Assert.Equal("Jira begrenzt gerade die Anfragen. Bitte in einer Minute erneut versuchen.", vm.ErrorMessage);
        Assert.True(vm.HasError);
    }

    [Fact]
    public async Task Unavailable_jira_sets_error_message_and_refresh_clears_it()
    {
        var service = new FakeWorklogService { ThrowOnce = new JiraUnavailableException("offline") };
        var vm = CreateViewModel(service);

        await vm.LoadAsync();
        Assert.Equal("Jira ist nicht erreichbar. Netzwerkverbindung prüfen.", vm.ErrorMessage);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task Failed_issues_set_warning_message()
    {
        var service = new FakeWorklogService
        {
            Respond = (_, _) => new WorklogFetchResult(Array.Empty<WorklogEntry>(), new[] { "ABC-3", "ABC-9" }),
        };
        var vm = CreateViewModel(service);

        await vm.LoadAsync();

        Assert.Equal("Für 2 Ticket(s) konnten die Buchungen nicht geladen werden: ABC-3, ABC-9", vm.WarningMessage);
        Assert.True(vm.HasWarning);
    }

    [Fact]
    public void Logout_raises_LogoutRequested()
    {
        var vm = CreateViewModel(new FakeWorklogService());
        var requested = false;
        vm.LogoutRequested += () => requested = true;

        vm.LogoutCommand.Execute(null);

        Assert.True(requested);
    }

    [Fact]
    public async Task Overlapping_load_that_finishes_after_switching_weeks_does_not_overwrite_the_display()
    {
        var service = new FakeWorklogService
        {
            Respond = (from, _) => from == new DateOnly(2026, 9, 14)
                ? new WorklogFetchResult(new[] { Entry(16, 9, 500) }, Array.Empty<string>())
                : new WorklogFetchResult(new[] { Entry(23, 9, 60) }, Array.Empty<string>()),
        };
        var gate = new TaskCompletionSource();
        service.Gate = gate;
        var vm = CreateViewModel(service);

        // Erster Abruf (Woche 14.-20.09.) hängt am Gate, bis wir ihn gezielt freigeben.
        var staleLoad = vm.LoadAsync();

        // Nutzer wechselt, während der erste Abruf noch läuft, in eine andere Woche.
        service.Gate = null;
        vm.SelectedDate = new DateOnly(2026, 9, 23);
        await vm.LoadAsync();

        Assert.Equal("1h", vm.Day.TotalText);

        // Der alte, veraltete Abruf kommt jetzt verspätet zurück.
        gate.SetResult();
        await staleLoad;

        Assert.Equal("1h", vm.Day.TotalText);

        // Der Zwischenspeicher darf nicht auf die alte Woche zurückfallen: ein weiterer
        // Abruf für die aktuelle Woche darf keinen neuen Netzwerkzugriff auslösen.
        await vm.LoadAsync();
        Assert.Equal(2, service.Calls.Count);
    }

    [Fact]
    public async Task IsLoading_is_false_after_overlapping_loads_settle()
    {
        var service = new FakeWorklogService();
        var gate = new TaskCompletionSource();
        service.Gate = gate;
        var vm = CreateViewModel(service);

        var staleLoad = vm.LoadAsync();
        Assert.True(vm.IsLoading);

        service.Gate = null;
        vm.SelectedDate = new DateOnly(2026, 9, 23);
        await vm.LoadAsync();

        Assert.False(vm.IsLoading);

        gate.SetResult();
        await staleLoad;

        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task Logout_during_a_running_load_does_not_leave_IsLoading_stuck()
    {
        var service = new FakeWorklogService();
        var gate = new TaskCompletionSource();
        service.Gate = gate;
        var vm = CreateViewModel(service);

        var runningLoad = vm.LoadAsync();
        Assert.True(vm.IsLoading);

        vm.LogoutCommand.Execute(null);
        gate.SetResult();
        await runningLoad;

        Assert.False(vm.IsLoading);
    }

    [Fact]
    public void UserInitials_are_derived_from_the_display_name()
    {
        var vm = CreateViewModel(new FakeWorklogService());

        Assert.Equal("S", vm.UserInitials);
    }

    [Fact]
    public async Task Month_mode_first_load_requests_the_visible_rectangle()
    {
        var service = new FakeWorklogService();
        var vm = CreateViewModel(service);

        await vm.ShowMonthCommand.ExecuteAsync(null);

        Assert.True(vm.IsMonthMode);
        var call = Assert.Single(service.Calls);
        // September 2026: der Monatserste (01.09., Dienstag) liegt in der Woche ab Montag 31.08.;
        // der Monatsletzte (30.09., Mittwoch) liegt in der Woche bis Sonntag 04.10. — das sichtbare Rechteck.
        Assert.Equal((new DateOnly(2026, 8, 31), new DateOnly(2026, 10, 4)), call);
    }

    [Fact]
    public async Task Switching_from_month_to_day_within_the_same_month_does_not_reload()
    {
        var service = new FakeWorklogService();
        var vm = CreateViewModel(service);
        await vm.ShowMonthCommand.ExecuteAsync(null);

        vm.ShowDayCommand.Execute(null);

        Assert.True(vm.IsDayMode);
        Assert.Single(service.Calls);
    }

    [Fact]
    public async Task Arrow_in_month_mode_jumps_to_the_first_of_the_previous_month()
    {
        var service = new FakeWorklogService();
        var vm = CreateViewModel(service);
        await vm.ShowMonthCommand.ExecuteAsync(null);

        await vm.PreviousCommand.ExecuteAsync(null);

        Assert.Equal(new DateOnly(2026, 8, 1), vm.SelectedDate);
        Assert.Equal("August 2026", vm.PeriodLabel);
        Assert.Equal(2, service.Calls.Count);
        Assert.Equal((new DateOnly(2026, 7, 27), new DateOnly(2026, 9, 6)), service.Calls[1]);
    }

    [Fact]
    public async Task Month_mode_shows_month_and_year_as_period_label()
    {
        var vm = CreateViewModel(new FakeWorklogService());

        await vm.ShowMonthCommand.ExecuteAsync(null);

        Assert.Equal("September 2026", vm.PeriodLabel);
    }

    [Fact]
    public void Week_label_spans_month_boundary_when_the_week_crosses_it()
    {
        var vm = CreateViewModel(new FakeWorklogService());
        vm.ShowWeekCommand.Execute(null);

        vm.SelectedDate = new DateOnly(2026, 9, 28);

        Assert.Equal("28. September – 4. Oktober 2026", vm.PeriodLabel);
    }

    [Fact]
    public async Task Stats_and_rows_are_cleared_while_a_new_period_is_loading()
    {
        // Befund B, Fall 1: PeriodLabel wechselt beim Blättern sofort, Stats erst nach dem Abruf.
        // Während des Ladens stand deshalb der neue Zeitraumtitel über den Zahlen des alten.
        var service = new FakeWorklogService
        {
            Respond = (_, _) => new WorklogFetchResult(new[] { Entry(16, 9, 240) }, Array.Empty<string>()),
        };
        var vm = CreateViewModel(service);
        await vm.LoadAsync();

        Assert.Equal("4h", vm.Stats.LoggedText);
        Assert.Single(vm.Day.Rows);

        // Abruf der nächsten Woche anhalten, damit der Zustand mitten im Laden geprüft werden kann.
        var gate = new TaskCompletionSource();
        service.Gate = gate;
        vm.SelectedDate = new DateOnly(2026, 9, 23); // andere Woche: löst einen echten Abruf aus
        var pending = vm.LoadAsync();

        Assert.True(vm.IsLoading);
        Assert.Equal("23. September 2026", vm.PeriodLabel);
        Assert.Equal(TimeSpan.Zero, vm.Stats.Logged);
        Assert.Equal("0m", vm.Stats.LoggedText);
        Assert.Empty(vm.Day.Rows);
        Assert.Empty(vm.Day.IssueTotals);

        gate.SetResult();
        await pending;
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task Stats_and_rows_are_cleared_after_a_failed_load()
    {
        // Befund B, Fall 2: Der Fehlerzweig verlässt LoadAsync ohne Render(). Ohne das Verwerfen zu
        // Beginn des Abrufs blieben die Zahlen und Buchungen des alten Zeitraums dauerhaft unter dem
        // Titel des neuen stehen — sichtbar nur das allgemeine Fehlerbanner.
        var service = new FakeWorklogService
        {
            Respond = (_, _) => new WorklogFetchResult(new[] { Entry(16, 9, 240) }, Array.Empty<string>()),
        };
        var vm = CreateViewModel(service);
        await vm.LoadAsync();

        Assert.Equal("4h", vm.Stats.LoggedText);
        Assert.Single(vm.Day.Rows);

        service.ThrowOnce = new JiraUnavailableException("offline");
        vm.SelectedDate = new DateOnly(2026, 9, 23); // andere Woche
        await vm.LoadAsync();

        Assert.Equal("Jira ist nicht erreichbar. Netzwerkverbindung prüfen.", vm.ErrorMessage);
        Assert.Equal("23. September 2026", vm.PeriodLabel);
        Assert.Equal(TimeSpan.Zero, vm.Stats.Logged);
        Assert.Equal("0m", vm.Stats.LoggedText);
        Assert.Empty(vm.Day.Rows);
        Assert.Empty(vm.Day.IssueTotals);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task Stats_reflect_the_displayed_period_for_day_week_and_month()
    {
        var service = new FakeWorklogService
        {
            Respond = (_, _) => new WorklogFetchResult(
                new[]
                {
                    Entry(16, 9, 240), // Mi 16.09., 4h
                    Entry(17, 9, 120), // Do 17.09., 2h – dieselbe Woche
                    Entry(20, 9, 60), // So 20.09., 1h – dieselbe Woche, Wochenende
                    Entry(25, 9, 180), // Fr 25.09., 3h – anderer Woche, aber selber Kalendermonat
                },
                Array.Empty<string>()),
        };
        var vm = CreateViewModel(service);

        await vm.ShowMonthCommand.ExecuteAsync(null); // lädt das ganze sichtbare Rechteck inkl. aller Einträge

        Assert.Equal("6 %", vm.Stats.PercentText);
        Assert.Equal("−166h", vm.Stats.DifferenceText);

        vm.ShowWeekCommand.Execute(null);

        Assert.Equal("18 %", vm.Stats.PercentText);
        Assert.Equal("−33h", vm.Stats.DifferenceText);

        vm.ShowDayCommand.Execute(null); // SelectedDate bleibt der 16. (Mittwoch)

        Assert.Equal("50 %", vm.Stats.PercentText);
        Assert.Equal("−4h", vm.Stats.DifferenceText);
    }

    [Fact]
    public async Task Month_view_model_grid_has_one_row_per_visible_week_with_seven_days_each()
    {
        var vm = CreateViewModel(new FakeWorklogService());

        await vm.ShowMonthCommand.ExecuteAsync(null);

        Assert.Equal(5, vm.Month.Weeks.Count); // September 2026: 31.08.–04.10. = 35 Tage = 5 Wochen
        Assert.All(vm.Month.Weeks, week => Assert.Equal(7, week.Days.Count));
    }

    [Fact]
    public async Task Week_day_bar_ratio_uses_the_daily_target_or_the_week_maximum_when_the_target_is_zero()
    {
        var service = new FakeWorklogService
        {
            Respond = (_, _) => new WorklogFetchResult(
                new[] { Entry(16, 9, 240), Entry(19, 9, 180) }, // Mi 4h (Soll 8h), Sa 3h (Soll 0h)
                Array.Empty<string>()),
        };
        var vm = CreateViewModel(service);

        await vm.LoadAsync();

        Assert.Equal(0.5, vm.Week.Days[2].BarRatio, 3); // Mittwoch: 4h von 8h Soll
        Assert.Equal(0.75, vm.Week.Days[5].BarRatio, 3); // Samstag: Soll 0 -> bemisst sich am Wochenmaximum (4h)
    }
}
