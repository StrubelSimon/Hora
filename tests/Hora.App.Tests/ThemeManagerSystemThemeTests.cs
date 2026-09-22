using System.Collections.ObjectModel;
using System.Windows;
using Hora.App.Services;
using Hora.App.Tests.TestDoubles;

namespace Hora.App.Tests;

/// <summary>
/// Die dritte Wahl „System“: Der wirksame Modus folgt der Windows-Einstellung für Apps, und zwar
/// auch dann noch, wenn der Nutzer sie im laufenden Betrieb umstellt. Eine ausdrückliche Wahl
/// („Hell“ oder „Dunkel“) verlässt „System“ und hört danach nicht mehr auf Windows.
/// </summary>
public class ThemeManagerSystemThemeTests
{
    private static ThemeManager Create(FakeSystemThemeReader systemTheme, out Collection<ResourceDictionary> merged)
    {
        merged = new Collection<ResourceDictionary>();
        return new ThemeManager(merged, systemTheme);
    }

    [StaFact]
    public void System_resolves_to_light_when_windows_is_light()
    {
        StaRunner.Run(() =>
        {
            var manager = Create(new FakeSystemThemeReader(AppTheme.Light), out _);

            manager.ApplyPreference(ThemePreference.System);

            Assert.Equal(ThemePreference.System, manager.Preference);
            Assert.Equal(AppTheme.Light, manager.Current);
        });
    }

    [StaFact]
    public void System_resolves_to_dark_when_windows_is_dark()
    {
        StaRunner.Run(() =>
        {
            var manager = Create(new FakeSystemThemeReader(AppTheme.Dark), out _);

            manager.ApplyPreference(ThemePreference.System);

            Assert.Equal(ThemePreference.System, manager.Preference);
            Assert.Equal(AppTheme.Dark, manager.Current);
        });
    }

    [StaFact]
    public void A_change_of_the_windows_setting_is_followed_while_system_is_chosen()
    {
        StaRunner.Run(() =>
        {
            var windows = new FakeSystemThemeReader(AppTheme.Light);
            var manager = Create(windows, out var merged);
            manager.ApplyPreference(ThemePreference.System);
            var reported = new List<AppTheme>();
            manager.Changed += theme => reported.Add(theme);

            windows.Set(AppTheme.Dark);

            Assert.Equal(AppTheme.Dark, manager.Current);
            Assert.Equal(ThemePreference.System, manager.Preference); // die Wahl bleibt „System“
            Assert.Equal(new[] { AppTheme.Dark }, reported);
            Assert.Single(merged); // kein Stapeln von Wörterbüchern
        });
    }

    [StaFact]
    public void A_change_of_the_windows_setting_is_ignored_after_an_explicit_choice()
    {
        StaRunner.Run(() =>
        {
            var windows = new FakeSystemThemeReader(AppTheme.Light);
            var manager = Create(windows, out _);
            manager.ApplyPreference(ThemePreference.Light);
            var reported = new List<AppTheme>();
            manager.Changed += theme => reported.Add(theme);

            windows.Set(AppTheme.Dark);

            Assert.Equal(AppTheme.Light, manager.Current);
            Assert.Empty(reported);
        });
    }

    [StaFact]
    public void An_explicit_choice_leaves_system()
    {
        StaRunner.Run(() =>
        {
            var manager = Create(new FakeSystemThemeReader(AppTheme.Dark), out _);
            manager.ApplyPreference(ThemePreference.System);

            manager.Apply(AppTheme.Light);

            Assert.Equal(ThemePreference.Light, manager.Preference);
            Assert.Equal(AppTheme.Light, manager.Current);
        });
    }

    [StaFact]
    public void Toggle_leaves_system_and_takes_the_opposite_of_the_effective_theme()
    {
        StaRunner.Run(() =>
        {
            // Das tut die Pille in der Fensterleiste: Sie schaltet auf den Zielmodus um, den sie
            // gerade anzeigt — und der bezieht sich auf das wirksame Aussehen.
            var manager = Create(new FakeSystemThemeReader(AppTheme.Dark), out _);
            manager.ApplyPreference(ThemePreference.System);

            manager.Toggle();

            Assert.Equal(ThemePreference.Light, manager.Preference);
            Assert.Equal(AppTheme.Light, manager.Current);
        });
    }

    [StaFact]
    public void Dispose_unsubscribes_from_the_system_theme_reader()
    {
        StaRunner.Run(() =>
        {
            var windows = new FakeSystemThemeReader(AppTheme.Light);
            var manager = Create(windows, out _);
            manager.ApplyPreference(ThemePreference.System);
            Assert.Equal(1, windows.Listeners);

            manager.Dispose();

            Assert.Equal(0, windows.Listeners);
            windows.Set(AppTheme.Dark);
            Assert.Equal(AppTheme.Light, manager.Current);
        });
    }

    [StaFact]
    public void Without_a_reader_system_falls_back_to_light()
    {
        StaRunner.Run(() =>
        {
            // Kein Zugriff auf die Windows-Einstellung: hell annehmen, wie beim unlesbaren Wert.
            var merged = new Collection<ResourceDictionary>();
            var manager = new ThemeManager(merged);

            manager.ApplyPreference(ThemePreference.System);

            Assert.Equal(ThemePreference.System, manager.Preference);
            Assert.Equal(AppTheme.Light, manager.Current);
        });
    }

    [StaFact]
    public void The_stored_choice_system_is_restored_on_the_next_start()
    {
        StaRunner.Run(() =>
        {
            // Wie App.OnStartup: gespeicherte Wahl lesen, auflösen, anwenden.
            var windows = new FakeSystemThemeReader(AppTheme.Dark);
            var manager = Create(windows, out _);

            manager.ApplyPreference(ThemePreferenceNames.Parse("System"));

            Assert.Equal(ThemePreference.System, manager.Preference);
            Assert.Equal(AppTheme.Dark, manager.Current);
        });
    }
}
