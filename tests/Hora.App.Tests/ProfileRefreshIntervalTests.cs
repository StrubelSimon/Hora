using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.Core.Jira.Models;
using Hora.Core.Storage;

namespace Hora.App.Tests;

/// <summary>
/// Die Zeile „Automatisch aktualisieren“ im Abschnitt JIRA der Profilseite: ein Segmentschalter
/// „Aus | 5m | 15m | 30m“. Vorbelegt sind fünf Minuten. Wie alles auf dieser Seite wirkt die Wahl
/// sofort und wird sofort gespeichert.
/// </summary>
public class ProfileRefreshIntervalTests
{
    private static readonly JiraUser User = new()
    {
        AccountId = "me",
        DisplayName = "Simon Strubel",
        EmailAddress = "simon.strubel@advisori.de",
    };

    private static (ProfileViewModel profile, FakeSettingsStore store) Create(AppSettings? settings = null)
    {
        var store = new FakeSettingsStore { Settings = settings ?? new AppSettings() };
        var profile = new ProfileViewModel(store, User, "https://example.atlassian.net", store.Settings.Schedule);
        return (profile, store);
    }

    [Fact]
    public void Five_minutes_is_the_default()
    {
        var (profile, _) = Create();

        Assert.Equal(5, profile.RefreshIntervalMinutes);
        Assert.True(profile.IsFiveMinuteRefresh);
        Assert.False(profile.IsRefreshOff);
        Assert.Equal("Alle 5 Minuten", profile.RefreshIntervalText);
    }

    [Fact]
    public void Choosing_another_interval_stores_it_at_once()
    {
        var (profile, store) = Create();

        profile.UseFifteenMinuteRefreshCommand.Execute(null);

        Assert.Equal(15, store.Settings.RefreshIntervalMinutes);
        Assert.True(profile.IsFifteenMinuteRefresh);
        Assert.False(profile.IsFiveMinuteRefresh);
        Assert.Equal("Alle 15 Minuten", profile.RefreshIntervalText);
    }

    [Fact]
    public void Turning_it_off_stores_zero()
    {
        var (profile, store) = Create();

        profile.TurnRefreshOffCommand.Execute(null);

        Assert.Equal(0, store.Settings.RefreshIntervalMinutes);
        Assert.True(profile.IsRefreshOff);
        Assert.Equal("Ausgeschaltet", profile.RefreshIntervalText);
    }

    [Fact]
    public void Choosing_an_interval_tells_the_overview()
    {
        // Dasselbe Ereignis, über das auch Zeitformat und Sollstunden die Übersicht erreichen.
        var (profile, _) = Create();
        var notified = 0;
        profile.DisplaySettingsChanged += () => notified++;

        profile.UseThirtyMinuteRefreshCommand.Execute(null);

        Assert.Equal(1, notified);
        Assert.Equal(30, profile.RefreshIntervalMinutes);
    }

    [Fact]
    public void Choosing_the_interval_that_is_already_set_changes_nothing()
    {
        var (profile, _) = Create();
        var notified = 0;
        profile.DisplaySettingsChanged += () => notified++;

        profile.UseFiveMinuteRefreshCommand.Execute(null);

        Assert.Equal(0, notified);
    }

    [Fact]
    public void An_unknown_stored_interval_falls_back_to_five_minutes()
    {
        // Eine von Hand verdorbene settings.json darf weder die Seite noch den Takt kosten.
        var (profile, _) = Create(new AppSettings { RefreshIntervalMinutes = 7 });

        Assert.Equal(5, profile.RefreshIntervalMinutes);
        Assert.True(profile.IsFiveMinuteRefresh);
    }
}
