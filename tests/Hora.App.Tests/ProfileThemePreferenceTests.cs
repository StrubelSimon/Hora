using Hora.App.Services;
using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.Core.Jira.Models;
using Hora.Core.Storage;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

/// <summary>
/// Der Segmentschalter „System | Hell | Dunkel“ auf der Profilseite. Er zeigt die gespeicherte
/// Wahl, nicht das wirksame Ergebnis: Steht er auf „System“, bleibt er dort, gleich ob Windows
/// gerade hell oder dunkel ist.
/// </summary>
public class ProfileThemePreferenceTests
{
    private static readonly JiraUser User = new()
    {
        AccountId = "me",
        DisplayName = "Simon Strubel",
        EmailAddress = "simon.strubel@advisori.de",
    };

    private static (ProfileViewModel profile, FakeSettingsStore store, FakeThemeSwitcher theme) Create(
        ThemePreference stored = ThemePreference.Light,
        AppTheme windows = AppTheme.Light)
    {
        var store = new FakeSettingsStore
        {
            Settings = new AppSettings { Theme = ThemePreferenceNames.NameOf(stored) },
        };

        // Wie beim Programmstart: Die gespeicherte Wahl liegt beim Designwechsler, bevor die
        // Profilseite überhaupt entsteht.
        var theme = new FakeThemeSwitcher(windows);
        theme.ApplyPreference(stored);

        var profile = new ProfileViewModel(store, User, "https://example.atlassian.net", WorkSchedule.Default(), theme);
        return (profile, store, theme);
    }

    [Fact]
    public void The_segment_shows_the_stored_choice_and_not_the_effective_result()
    {
        // Wahl „System“, Windows steht auf dunkel: wirksam ist dunkel, gewählt ist „System“.
        var (profile, _, theme) = Create(ThemePreference.System, AppTheme.Dark);

        Assert.True(profile.IsSystemTheme);
        Assert.False(profile.IsLightTheme);
        Assert.False(profile.IsDarkTheme);
        Assert.Equal(AppTheme.Dark, theme.Current);
    }

    [Fact]
    public void Choosing_system_applies_the_windows_setting_and_saves_the_choice()
    {
        var (profile, store, theme) = Create(ThemePreference.Light, AppTheme.Dark);

        profile.UseSystemThemeCommand.Execute(null);

        Assert.True(profile.IsSystemTheme);
        Assert.Equal(ThemePreference.System, theme.Preference);
        Assert.Equal(AppTheme.Dark, theme.Current);
        // Gespeichert wird die Wahl, nicht das Ergebnis.
        Assert.Equal("System", store.Settings.Theme);
    }

    [Fact]
    public void Choosing_system_twice_changes_nothing()
    {
        var (profile, store, theme) = Create(ThemePreference.System, AppTheme.Dark);
        var applyCallsBefore = theme.ApplyCalls;

        profile.UseSystemThemeCommand.Execute(null);

        Assert.Equal(applyCallsBefore, theme.ApplyCalls);
        Assert.Equal(0, store.SaveCalls);
    }

    [Fact]
    public void A_windows_change_moves_the_app_but_leaves_the_segment_on_system()
    {
        var (profile, store, theme) = Create(ThemePreference.System, AppTheme.Light);
        var changed = new List<string>();
        profile.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        theme.SetSystemTheme(AppTheme.Dark);

        Assert.Equal(AppTheme.Dark, theme.Current);
        Assert.True(profile.IsSystemTheme);
        Assert.False(profile.IsDarkTheme);
        Assert.Equal(0, store.SaveCalls); // die Wahl hat sich nicht geändert, es gibt nichts zu speichern
        Assert.Contains(nameof(ProfileViewModel.IsSystemTheme), changed);
    }

    [Fact]
    public void A_click_on_the_pill_leaves_system_and_moves_the_segment()
    {
        // Die Pille in der Fensterleiste bleibt sichtbar, während die Profilseite offen ist. Ein
        // Klick darauf setzt den ausdrücklichen Wert; „System“ ist damit verlassen.
        var (profile, _, theme) = Create(ThemePreference.System, AppTheme.Dark);
        var changed = new List<string>();
        profile.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        theme.Toggle();

        Assert.False(profile.IsSystemTheme);
        Assert.True(profile.IsLightTheme);
        Assert.Equal(ThemePreference.Light, theme.Preference);
        Assert.Contains(nameof(ProfileViewModel.IsSystemTheme), changed);
    }

    [Fact]
    public void Choosing_dark_while_system_is_active_leaves_system()
    {
        var (profile, store, theme) = Create(ThemePreference.System, AppTheme.Dark);

        profile.UseDarkThemeCommand.Execute(null);

        Assert.False(profile.IsSystemTheme);
        Assert.True(profile.IsDarkTheme);
        Assert.Equal(ThemePreference.Dark, theme.Preference);
        Assert.Equal("Dark", store.Settings.Theme);
    }

    [Fact]
    public void An_unknown_value_in_the_file_shows_the_light_segment()
    {
        var store = new FakeSettingsStore { Settings = new AppSettings { Theme = "Blau" } };
        var theme = new FakeThemeSwitcher();
        theme.ApplyPreference(ThemePreferenceNames.Parse(store.Settings.Theme));

        var profile = new ProfileViewModel(store, User, "https://example.atlassian.net", WorkSchedule.Default(), theme);

        Assert.True(profile.IsLightTheme);
        Assert.False(profile.IsSystemTheme);
    }

    [Fact]
    public void Detach_stops_listening_after_a_windows_change_too()
    {
        var (profile, _, theme) = Create(ThemePreference.System, AppTheme.Light);
        var changed = new List<string>();
        profile.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        profile.Detach();
        theme.SetSystemTheme(AppTheme.Dark);

        Assert.Empty(changed);
    }
}
