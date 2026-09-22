using Hora.App.Services;
using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.Core.Jira.Models;
using Hora.Core.Storage;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

/// <summary>
/// Der Weg von der Übersicht auf die Profilseite und zurück, und was dabei zwischen beiden Seiten
/// durchgereicht wird: Zeitformat, Sollstunden, Zeitpunkt des letzten erfolgreichen Abrufs.
/// </summary>
public class ProfileNavigationTests
{
    private static readonly StoredCredentials ValidCredentials = new("https://example.atlassian.net", "a@b.de", "tok");

    private static readonly DateOnly Today = new(2026, 9, 16);

    private static readonly DateTimeOffset Now = new(2026, 9, 17, 9, 12, 0, TimeSpan.FromHours(2));

    private static (MainViewModel vm, FakeSettingsStore settings, FakeCredentialStore credentials, FakeThemeSwitcher theme) Create(
        AppSettings? stored = null)
    {
        var settings = new FakeSettingsStore();
        if (stored is not null)
        {
            settings.Settings = stored;
        }

        var credentials = new FakeCredentialStore { Stored = ValidCredentials };
        var theme = new FakeThemeSwitcher();
        var vm = new MainViewModel(
            credentials,
            settings,
            new NullLogger(),
            (_, _, _) => new FakeJiraClient(),
            () => Today,
            theme,
            () => Now);

        return (vm, settings, credentials, theme);
    }

    // ===================== Avatar öffnet das Profil =====================

    [Fact]
    public async Task The_avatar_opens_the_profile_page_instead_of_logging_out()
    {
        var (vm, _, credentials, _) = Create();
        await vm.StartAsync();
        var overview = Assert.IsType<OverviewViewModel>(vm.CurrentScreen);

        overview.ShowProfileCommand.Execute(null);

        Assert.IsType<ProfileViewModel>(vm.CurrentScreen);
        Assert.Equal(0, credentials.DeleteCalls);
    }

    [Fact]
    public async Task Done_returns_to_the_very_same_overview()
    {
        var (vm, _, _, _) = Create();
        await vm.StartAsync();
        var overview = Assert.IsType<OverviewViewModel>(vm.CurrentScreen);
        overview.ShowProfileCommand.Execute(null);
        var profile = Assert.IsType<ProfileViewModel>(vm.CurrentScreen);

        profile.CloseCommand.Execute(null);

        Assert.Same(overview, vm.CurrentScreen);
    }

    [Fact]
    public async Task Logging_out_from_the_profile_deletes_the_credentials_and_shows_the_login()
    {
        var (vm, _, credentials, _) = Create();
        await vm.StartAsync();
        var overview = Assert.IsType<OverviewViewModel>(vm.CurrentScreen);
        overview.ShowProfileCommand.Execute(null);
        var profile = Assert.IsType<ProfileViewModel>(vm.CurrentScreen);

        profile.LogoutCommand.Execute(null);

        Assert.IsType<LoginViewModel>(vm.CurrentScreen);
        Assert.Equal(1, credentials.DeleteCalls);
    }

    // ===================== Startansicht =====================

    [Fact]
    public async Task The_overview_starts_in_the_mode_from_the_settings()
    {
        var (vm, _, _, _) = Create(new AppSettings { StartView = "Week" });

        await vm.StartAsync();

        var overview = Assert.IsType<OverviewViewModel>(vm.CurrentScreen);
        Assert.True(overview.IsWeekMode);
    }

    [Fact]
    public async Task An_unknown_start_view_falls_back_to_day()
    {
        var (vm, _, _, _) = Create(new AppSettings { StartView = "Jahrhundert" });

        await vm.StartAsync();

        var overview = Assert.IsType<OverviewViewModel>(vm.CurrentScreen);
        Assert.True(overview.IsDayMode);
    }

    // ===================== Zeitformat wirkt auf die ganze App =====================

    [Fact]
    public async Task The_overview_starts_with_the_time_format_from_the_settings()
    {
        var (vm, _, _, _) = Create(new AppSettings { TimeFormat = "DecimalHours" });

        await vm.StartAsync();

        var overview = Assert.IsType<OverviewViewModel>(vm.CurrentScreen);
        Assert.Equal(DurationFormat.DecimalHours, overview.DurationFormat);
    }

    [Fact]
    public async Task Choosing_a_time_format_on_the_profile_page_reaches_the_overview_at_once()
    {
        var (vm, _, _, _) = Create();
        await vm.StartAsync();
        var overview = Assert.IsType<OverviewViewModel>(vm.CurrentScreen);
        overview.ShowProfileCommand.Execute(null);
        var profile = Assert.IsType<ProfileViewModel>(vm.CurrentScreen);

        profile.UseDecimalHoursFormatCommand.Execute(null);

        Assert.Equal(DurationFormat.DecimalHours, overview.DurationFormat);
    }

    [Fact]
    public async Task Changing_the_target_hours_on_the_profile_page_moves_the_overview_numbers()
    {
        var (vm, _, _, _) = Create();
        await vm.StartAsync();
        var overview = Assert.IsType<OverviewViewModel>(vm.CurrentScreen);
        var targetBefore = overview.Stats.Target;
        overview.ShowProfileCommand.Execute(null);
        var profile = Assert.IsType<ProfileViewModel>(vm.CurrentScreen);

        profile.DecreaseTargetHoursCommand.Execute(null); // 8h -> 7,5h

        Assert.Equal(TimeSpan.FromHours(8), targetBefore);
        Assert.Equal(TimeSpan.FromHours(7.5), overview.Stats.Target);
    }

    // ===================== Letzte Synchronisation =====================

    [Fact]
    public async Task A_successful_fetch_stores_the_moment_of_the_last_synchronisation()
    {
        var (vm, settings, _, _) = Create();

        await vm.StartAsync();

        Assert.Equal(Now, settings.Settings.LastSyncAt);
    }

    [Fact]
    public async Task The_profile_page_shows_the_stored_moment_of_the_last_synchronisation()
    {
        var (vm, _, _, _) = Create();
        await vm.StartAsync();
        var overview = Assert.IsType<OverviewViewModel>(vm.CurrentScreen);

        overview.ShowProfileCommand.Execute(null);

        var profile = Assert.IsType<ProfileViewModel>(vm.CurrentScreen);
        Assert.Equal("17.09.2026, 09:12", profile.LastSyncText);
    }

    [Fact]
    public async Task Storing_the_moment_of_the_last_synchronisation_keeps_the_other_settings()
    {
        var (vm, settings, _, _) = Create(new AppSettings
        {
            Role = "Senior Consultant",
            StartView = "Week",
            TimeFormat = "DecimalHours",
            ReminderEnabled = true,
        });

        await vm.StartAsync();

        Assert.Equal(Now, settings.Settings.LastSyncAt);
        Assert.Equal("Senior Consultant", settings.Settings.Role);
        Assert.Equal("Week", settings.Settings.StartView);
        Assert.Equal("DecimalHours", settings.Settings.TimeFormat);
        Assert.True(settings.Settings.ReminderEnabled);
    }
}
