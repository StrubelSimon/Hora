using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.Core.Jira.Models;
using Hora.Core.Storage;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

/// <summary>
/// Die Zeile „Arbeitstage“ auf der Profilseite: sieben Umschalter „Mo Di Mi Do Fr Sa So“. Der
/// Schrittschalter „Sollstunden pro Tag“ gilt für alle ausgewählten Tage; ein abgewählter Tag steht
/// auf null. Alles wirkt sofort auf Soll und Differenz, weil die Seite dieselbe WorkSchedule-Instanz
/// verändert, an der auch die Übersicht hängt.
/// </summary>
public class ProfileWorkdayTests
{
    private static readonly JiraUser User = new()
    {
        AccountId = "me",
        DisplayName = "Simon Strubel",
        EmailAddress = "simon.strubel@advisori.de",
    };

    /// <summary>Eine Woche vom Montag bis zum Sonntag — für die Probe auf Soll und Differenz.</summary>
    private static readonly DateOnly WeekStart = new(2026, 9, 14);

    private static readonly DateOnly WeekEnd = new(2026, 9, 20);

    private static (ProfileViewModel profile, FakeSettingsStore store, WorkSchedule schedule) Create(
        WorkSchedule? schedule = null)
    {
        var live = schedule ?? WorkSchedule.Default();

        // Wie im laufenden Programm: Die Übersicht reicht genau die gespeicherte Woche als lebende
        // Instanz an die Profilseite weiter.
        var store = new FakeSettingsStore { Settings = new AppSettings { Schedule = live } };
        var profile = new ProfileViewModel(store, User, "https://example.atlassian.net", live);
        return (profile, store, live);
    }

    private static WorkdayToggleViewModel ToggleFor(ProfileViewModel profile, DayOfWeek day) =>
        profile.Workdays.Single(workday => workday.Day == day);

    [Fact]
    public void Seven_toggles_stand_in_the_german_week_order()
    {
        var (profile, _, _) = Create();

        Assert.Equal(
            new[] { "Mo", "Di", "Mi", "Do", "Fr", "Sa", "So" },
            profile.Workdays.Select(workday => workday.Label));
        Assert.Equal(
            new[]
            {
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday,
                DayOfWeek.Saturday,
                DayOfWeek.Sunday,
            },
            profile.Workdays.Select(workday => workday.Day));
    }

    [Fact]
    public void The_selection_comes_from_the_stored_week()
    {
        var (profile, store, _) = Create();

        Assert.Equal(
            new[] { true, true, true, true, true, false, false },
            profile.Workdays.Select(workday => workday.IsSelected));
        Assert.Equal("5 Tage pro Woche", profile.WorkdayCountText);
        Assert.Equal(0, store.SaveCalls); // reines Anzeigen speichert nichts
    }

    [Fact]
    public void Deselecting_a_day_sets_it_to_zero_and_saves_immediately()
    {
        var (profile, store, schedule) = Create();

        ToggleFor(profile, DayOfWeek.Friday).IsSelected = false;

        Assert.Equal(0, schedule.Friday);
        Assert.Equal(8, schedule.Monday);
        Assert.Equal("4 Tage pro Woche", profile.WorkdayCountText);
        Assert.Equal(0, store.Settings.Schedule.Friday);
        Assert.Equal(1, store.SaveCalls);
    }

    [Fact]
    public void Selecting_a_day_gives_it_the_current_stepper_value()
    {
        var (profile, store, schedule) = Create();

        ToggleFor(profile, DayOfWeek.Saturday).IsSelected = true;

        Assert.Equal(8, schedule.Saturday);
        Assert.Equal("6 Tage pro Woche", profile.WorkdayCountText);
        Assert.Equal(8, store.Settings.Schedule.Saturday);
    }

    [Fact]
    public void A_deselected_day_comes_back_with_the_value_of_the_stepper()
    {
        var (profile, _, schedule) = Create();
        profile.DecreaseTargetHoursCommand.Execute(null); // 8h -> 7,5h für alle ausgewählten Tage

        ToggleFor(profile, DayOfWeek.Monday).IsSelected = false;
        ToggleFor(profile, DayOfWeek.Monday).IsSelected = true;

        Assert.Equal(7.5, schedule.Monday);
    }

    [Fact]
    public void The_sub_line_uses_the_singular_for_exactly_one_day()
    {
        var (profile, _, _) = Create();

        foreach (var day in new[] { DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            ToggleFor(profile, day).IsSelected = false;
        }

        Assert.Equal("1 Tag pro Woche", profile.WorkdayCountText);
    }

    [Fact]
    public void The_sub_line_is_reported_as_changed()
    {
        var (profile, _, _) = Create();
        var changed = new List<string>();
        profile.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        ToggleFor(profile, DayOfWeek.Saturday).IsSelected = true;

        Assert.Contains(nameof(ProfileViewModel.WorkdayCountText), changed);
    }

    [Fact]
    public void Without_any_workday_the_weekly_target_is_zero_and_nothing_divides_by_zero()
    {
        var (profile, _, schedule) = Create();

        foreach (var workday in profile.Workdays)
        {
            workday.IsSelected = false;
        }

        Assert.Equal("0 Tage pro Woche", profile.WorkdayCountText);

        var target = TargetCalculator.ForRange(schedule, WeekStart, WeekEnd);
        Assert.Equal(TimeSpan.Zero, target);

        var stats = PeriodStats.For(TimeSpan.FromHours(3), target);
        Assert.Equal("–", stats.PercentText);
        Assert.Equal(0, stats.Segments.TargetShare);
        Assert.Equal("+3h", stats.DifferenceText);
    }

    [Fact]
    public void The_stepper_applies_to_every_selected_day_including_the_weekend()
    {
        var (profile, _, schedule) = Create();
        ToggleFor(profile, DayOfWeek.Sunday).IsSelected = true;

        profile.DecreaseTargetHoursCommand.Execute(null);

        Assert.Equal(7.5, schedule.Monday);
        Assert.Equal(7.5, schedule.Friday);
        Assert.Equal(7.5, schedule.Sunday);
        Assert.Equal(0, schedule.Saturday); // nicht ausgewählt, bleibt null
    }

    [Fact]
    public void The_stepper_leaves_deselected_days_at_zero()
    {
        var (profile, _, schedule) = Create();
        ToggleFor(profile, DayOfWeek.Monday).IsSelected = false;

        profile.IncreaseTargetHoursCommand.Execute(null);

        Assert.Equal(0, schedule.Monday);
        Assert.Equal(8.5, schedule.Tuesday);
    }

    [Fact]
    public void Unequal_values_from_the_file_survive_as_long_as_nothing_is_touched()
    {
        var stored = new WorkSchedule { Monday = 0, Tuesday = 6, Wednesday = 7, Thursday = 8, Friday = 4, Saturday = 3 };

        var (profile, store, schedule) = Create(stored);

        Assert.Equal(
            new[] { false, true, true, true, true, true, false },
            profile.Workdays.Select(workday => workday.IsSelected));
        Assert.Equal("5 Tage pro Woche", profile.WorkdayCountText);
        Assert.Equal(6, profile.TargetHours); // der erste Arbeitstag, der nicht null ist
        Assert.Equal(0, schedule.Monday);
        Assert.Equal(7, schedule.Wednesday);
        Assert.Equal(3, schedule.Saturday);
        Assert.Equal(0, store.SaveCalls);
    }

    [Fact]
    public void A_change_takes_effect_on_target_and_difference_at_once()
    {
        // Dieselbe Instanz, an der die Übersicht rechnet: Was hier umgestellt wird, steht sofort in
        // den Kennzahlen.
        var (profile, _, schedule) = Create();
        var displayChanges = 0;
        profile.DisplaySettingsChanged += () => displayChanges++;

        var before = PeriodStats.For(TimeSpan.FromHours(32), TargetCalculator.ForRange(schedule, WeekStart, WeekEnd));
        Assert.Equal("40h", before.TargetText);
        Assert.Equal("−8h", before.DifferenceText);

        ToggleFor(profile, DayOfWeek.Friday).IsSelected = false;

        var after = PeriodStats.For(TimeSpan.FromHours(32), TargetCalculator.ForRange(schedule, WeekStart, WeekEnd));
        Assert.Equal("32h", after.TargetText);
        Assert.Equal("0h", after.DifferenceText);
        Assert.Equal(1, displayChanges);
    }

    [Fact]
    public void A_week_of_weekend_days_only_shows_its_own_hours_in_the_stepper()
    {
        var stored = new WorkSchedule { Saturday = 5, Sunday = 3 };

        var (profile, _, _) = Create(stored);

        Assert.Equal(5, profile.TargetHours);
        Assert.Equal("2 Tage pro Woche", profile.WorkdayCountText);
        Assert.Equal(
            new[] { false, false, false, false, false, true, true },
            profile.Workdays.Select(workday => workday.IsSelected));
    }
}
