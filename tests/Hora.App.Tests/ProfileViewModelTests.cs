using Hora.App.Services;
using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.Core.Jira.Models;
using Hora.Core.Storage;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

/// <summary>
/// Die Profilseite. Jede Änderung wirkt sofort und wird sofort gespeichert; es gibt keinen
/// Speichern-Knopf, „Fertig“ und der Zurück-Pfeil navigieren nur.
/// </summary>
public class ProfileViewModelTests
{
    private const string BaseUrl = "https://advisori-ftc.atlassian.net";

    private static readonly JiraUser User = new()
    {
        AccountId = "me",
        DisplayName = "Simon Strubel",
        EmailAddress = "simon.strubel@advisori.de",
    };

    private static (ProfileViewModel profile, FakeSettingsStore store, FakeThemeSwitcher theme, WorkSchedule schedule) Create(
        AppSettings? settings = null,
        WorkSchedule? schedule = null)
    {
        var store = new FakeSettingsStore();
        if (settings is not null)
        {
            store.Settings = settings;
        }

        var theme = new FakeThemeSwitcher();
        var liveSchedule = schedule ?? WorkSchedule.Default();
        var profile = new ProfileViewModel(store, User, BaseUrl, liveSchedule, theme);
        return (profile, store, theme, liveSchedule);
    }

    // ===================== Nutzerkarte =====================

    [Fact]
    public void User_card_shows_name_email_and_initials()
    {
        var (profile, _, _, _) = Create();

        Assert.Equal("Simon Strubel", profile.DisplayName);
        Assert.Equal("simon.strubel@advisori.de", profile.Email);
        Assert.Equal("SS", profile.Initials);
    }

    [Fact]
    public void The_role_badge_stays_hidden_while_the_role_is_empty()
    {
        var (profile, _, _, _) = Create();

        Assert.Equal(string.Empty, profile.Role);
        Assert.False(profile.HasRole);
    }

    [Fact]
    public void The_role_badge_shows_the_free_text_from_the_settings()
    {
        var (profile, _, _, _) = Create(new AppSettings { Role = "Senior Consultant" });

        Assert.Equal("Senior Consultant", profile.Role);
        Assert.True(profile.HasRole);
    }

    // ===================== Theme =====================

    [Fact]
    public void The_theme_segment_starts_on_the_currently_applied_theme()
    {
        var (profile, _, theme, _) = Create();

        Assert.True(profile.IsLightTheme);
        Assert.False(profile.IsDarkTheme);
        Assert.Equal(AppTheme.Light, theme.Current);
    }

    [Fact]
    public void Choosing_dark_applies_it_immediately_and_saves_it()
    {
        var (profile, store, theme, _) = Create();

        profile.UseDarkThemeCommand.Execute(null);

        Assert.Equal(AppTheme.Dark, theme.Current);
        Assert.True(profile.IsDarkTheme);
        Assert.False(profile.IsLightTheme);
        Assert.Equal("Dark", store.Settings.Theme);
    }

    [Fact]
    public void Choosing_the_theme_that_is_already_active_changes_nothing()
    {
        var (profile, _, theme, _) = Create();

        profile.UseLightThemeCommand.Execute(null);

        Assert.Equal(AppTheme.Light, theme.Current);
        Assert.Equal(0, theme.ApplyCalls);
    }

    [Fact]
    public void Switching_the_theme_elsewhere_moves_the_segment_along()
    {
        // Die Pille in der Fensterleiste bleibt sichtbar, während die Profilseite offen ist.
        // Beide Bedienelemente müssen denselben Zustand zeigen.
        var (profile, _, theme, _) = Create();
        var changed = new List<string>();
        profile.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        theme.Toggle();

        Assert.True(profile.IsDarkTheme);
        Assert.Contains(nameof(ProfileViewModel.IsDarkTheme), changed);
        Assert.Contains(nameof(ProfileViewModel.IsLightTheme), changed);
    }

    [Fact]
    public void Detach_stops_listening_to_the_theme_switcher()
    {
        // Der ThemeManager lebt so lange wie die App. Ohne Abmelden bliebe jede je geöffnete
        // Profilseite an seinem Changed-Ereignis hängen.
        var (profile, _, theme, _) = Create();
        var changed = new List<string>();
        profile.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        profile.Detach();
        theme.Toggle();

        Assert.Empty(changed);
    }

    // ===================== Startansicht =====================

    [Fact]
    public void The_start_view_defaults_to_day()
    {
        var (profile, _, _, _) = Create();

        Assert.True(profile.IsDayStartView);
        Assert.False(profile.IsWeekStartView);
        Assert.False(profile.IsMonthStartView);
    }

    [Fact]
    public void The_start_view_comes_from_the_settings()
    {
        var (profile, _, _, _) = Create(new AppSettings { StartView = "Month" });

        Assert.True(profile.IsMonthStartView);
    }

    [Fact]
    public void Choosing_a_start_view_saves_it_immediately()
    {
        var (profile, store, _, _) = Create();

        profile.UseWeekStartViewCommand.Execute(null);

        Assert.True(profile.IsWeekStartView);
        Assert.Equal("Week", store.Settings.StartView);
        Assert.Equal(1, store.SaveCalls);
    }

    // ===================== Zeitformat =====================

    [Fact]
    public void The_time_format_defaults_to_the_previous_rendering()
    {
        var (profile, _, _, _) = Create();

        Assert.True(profile.IsHoursMinutesFormat);
        Assert.False(profile.IsDecimalHoursFormat);
        Assert.Equal(DurationFormat.HoursMinutes, profile.DurationFormat);
    }

    [Fact]
    public void Choosing_decimal_hours_saves_it_and_reports_a_display_change()
    {
        var (profile, store, _, _) = Create();
        var displayChanges = 0;
        profile.DisplaySettingsChanged += () => displayChanges++;

        profile.UseDecimalHoursFormatCommand.Execute(null);

        Assert.True(profile.IsDecimalHoursFormat);
        Assert.Equal(DurationFormat.DecimalHours, profile.DurationFormat);
        Assert.Equal("DecimalHours", store.Settings.TimeFormat);
        Assert.Equal(1, displayChanges);
    }

    // ===================== Sollstunden pro Tag =====================

    [Fact]
    public void The_target_hours_show_the_uniform_value_of_the_schedule()
    {
        var (profile, _, _, _) = Create();

        Assert.Equal(8, profile.TargetHours);
        Assert.Equal("8h", profile.TargetHoursText);
    }

    [Fact]
    public void The_stepper_moves_in_half_hours()
    {
        var (profile, _, _, _) = Create();

        profile.DecreaseTargetHoursCommand.Execute(null);

        Assert.Equal(7.5, profile.TargetHours);
        Assert.Equal("7h 30m", profile.TargetHoursText);

        profile.IncreaseTargetHoursCommand.Execute(null);
        profile.IncreaseTargetHoursCommand.Execute(null);

        Assert.Equal(8.5, profile.TargetHours);
    }

    [Fact]
    public void The_stepper_stops_at_zero_and_at_twentyfour()
    {
        var (profile, _, _, _) = Create(schedule: new WorkSchedule());

        Assert.Equal(0, profile.TargetHours);
        profile.DecreaseTargetHoursCommand.Execute(null);
        Assert.Equal(0, profile.TargetHours);

        for (var step = 0; step < 60; step++)
        {
            profile.IncreaseTargetHoursCommand.Execute(null);
        }

        Assert.Equal(24, profile.TargetHours);
    }

    [Fact]
    public void The_stepper_sets_monday_to_friday_and_leaves_the_weekend_at_zero()
    {
        var schedule = WorkSchedule.Default();
        var (profile, store, _, _) = Create(schedule: schedule);

        profile.DecreaseTargetHoursCommand.Execute(null);

        // Die lebende Instanz, an der auch die Übersicht hängt, ändert sich mit …
        Assert.Equal(7.5, schedule.Monday);
        Assert.Equal(7.5, schedule.Friday);
        Assert.Equal(0, schedule.Saturday);
        Assert.Equal(0, schedule.Sunday);
        // … und der neue Stand steht sofort in den Einstellungen.
        Assert.Equal(7.5, store.Settings.Schedule.Monday);
        Assert.Equal(1, store.SaveCalls);
    }

    [Fact]
    public void Unequal_weekday_hours_are_shown_as_the_first_weekday_that_is_not_zero()
    {
        var schedule = new WorkSchedule { Monday = 0, Tuesday = 6, Wednesday = 7, Thursday = 8, Friday = 4 };

        var (profile, store, _, _) = Create(schedule: schedule);

        Assert.Equal(6, profile.TargetHours);
        // Solange der Schrittschalter nicht benutzt wird, bleibt die ungleiche Woche unangetastet.
        Assert.Equal(0, schedule.Monday);
        Assert.Equal(7, schedule.Wednesday);
        Assert.Equal(4, schedule.Friday);
        Assert.Equal(0, store.SaveCalls);
    }

    [Fact]
    public void Using_the_stepper_levels_a_previously_unequal_week()
    {
        var schedule = new WorkSchedule { Monday = 0, Tuesday = 6, Wednesday = 7, Thursday = 8, Friday = 4 };
        var (profile, _, _, _) = Create(schedule: schedule);

        profile.IncreaseTargetHoursCommand.Execute(null);

        Assert.Equal(6.5, profile.TargetHours);
        Assert.Equal(6.5, schedule.Monday);
        Assert.Equal(6.5, schedule.Friday);
        Assert.Equal(0, schedule.Sunday);
    }

    [Fact]
    public void The_target_hours_use_the_same_time_format_as_the_rest_of_the_app()
    {
        var (profile, _, _, _) = Create(new AppSettings { TimeFormat = "DecimalHours" });

        profile.DecreaseTargetHoursCommand.Execute(null); // 8h -> 7,5h

        Assert.Equal("7,50h", profile.TargetHoursText);
    }

    [Fact]
    public void Switching_the_time_format_updates_the_target_hours_text()
    {
        var (profile, _, _, _) = Create();
        profile.DecreaseTargetHoursCommand.Execute(null); // 7h 30m

        profile.UseDecimalHoursFormatCommand.Execute(null);

        Assert.Equal("7,50h", profile.TargetHoursText);
    }

    [Fact]
    public void Changing_the_target_hours_reports_a_display_change()
    {
        var (profile, _, _, _) = Create();
        var displayChanges = 0;
        profile.DisplaySettingsChanged += () => displayChanges++;

        profile.IncreaseTargetHoursCommand.Execute(null);

        Assert.Equal(1, displayChanges);
    }

    // ===================== Erinnerung =====================

    [Fact]
    public void The_reminder_is_off_by_default_and_only_stores_the_choice()
    {
        var (profile, store, _, _) = Create();

        Assert.False(profile.ReminderEnabled);

        profile.ReminderEnabled = true;

        Assert.True(store.Settings.ReminderEnabled);
        Assert.Equal(1, store.SaveCalls);
    }

    [Fact]
    public void The_reminder_state_comes_from_the_settings()
    {
        var (profile, _, _, _) = Create(new AppSettings { ReminderEnabled = true });

        Assert.True(profile.ReminderEnabled);
    }

    [Fact]
    public void Reading_the_reminder_state_does_not_save_anything()
    {
        var (profile, store, _, _) = Create(new AppSettings { ReminderEnabled = true });

        _ = profile.ReminderEnabled;

        Assert.Equal(0, store.SaveCalls);
    }

    // ===================== Jira =====================

    [Fact]
    public void The_instance_is_shown_without_the_scheme()
    {
        var (profile, _, _, _) = Create();

        Assert.Equal("advisori-ftc.atlassian.net", profile.InstanceText);
        Assert.Equal(BaseUrl, profile.InstanceUrl);
    }

    [Fact]
    public void A_missing_last_sync_shows_a_dash()
    {
        var (profile, _, _, _) = Create();

        Assert.Equal("–", profile.LastSyncText);
    }

    [Fact]
    public void The_last_sync_is_shown_in_the_german_short_form()
    {
        var settings = new AppSettings { LastSyncAt = new DateTimeOffset(2026, 9, 17, 9, 12, 0, TimeSpan.FromHours(2)) };

        var (profile, _, _, _) = Create(settings);

        Assert.Equal("17.09.2026, 09:12", profile.LastSyncText);
    }

    // ===================== Navigation =====================

    [Fact]
    public void Done_and_the_back_arrow_both_only_navigate_back()
    {
        var (profile, store, _, _) = Create();
        var closeRequests = 0;
        profile.CloseRequested += () => closeRequests++;

        profile.CloseCommand.Execute(null);
        profile.CloseCommand.Execute(null);

        Assert.Equal(2, closeRequests);
        Assert.Equal(0, store.SaveCalls); // kein Speichern-Knopf: gespeichert wird bei jeder Änderung
    }

    [Fact]
    public void Logout_asks_the_shell_to_log_out()
    {
        var (profile, _, _, _) = Create();
        var logoutRequests = 0;
        profile.LogoutRequested += () => logoutRequests++;

        profile.LogoutCommand.Execute(null);

        Assert.Equal(1, logoutRequests);
    }

    /// <summary>
    /// Die Profilseite zeigt die Version der laufenden Fassung. Sie kommt aus derselben Quelle wie
    /// der Fenstertitel – &lt;Version&gt; in Hora.App.csproj –, damit beide nicht auseinanderlaufen.
    /// </summary>
    [Fact]
    public void The_profile_page_shows_the_version_of_the_running_build()
    {
        var (profile, _, _, _) = Create();

        Assert.Equal(AppVersion.Current, profile.VersionText);

        // Drei durch Punkte getrennte Zahlen: Der Wert ist zum Vorlesen gedacht, nicht die
        // vierstellige Assembly-Schreibweise mit angehängter Null.
        var parts = profile.VersionText.Split('.');
        Assert.Equal(3, parts.Length);
        Assert.All(parts, part => Assert.True(
            part.Length > 0 && part.All(char.IsDigit),
            $"„{profile.VersionText}“ ist keine Versionsnummer aus drei Zahlen."));
    }
}
