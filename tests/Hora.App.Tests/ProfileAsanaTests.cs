using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.Core.Jira.Models;
using Hora.Core.Storage;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

public class ProfileAsanaTests
{
    private static ProfileViewModel Build(FakeAsanaCredentialStore asanaStore) => new(
        new FakeSettingsStore(),
        new JiraUser { AccountId = "1", DisplayName = "Simon Strubel", EmailAddress = "a@b.de" },
        "https://example.atlassian.net",
        WorkSchedule.Default(),
        themeSwitcher: null,
        asanaCredentialStore: asanaStore);

    [Fact]
    public void WithoutConnectionItSaysSoAndOffersConnecting()
    {
        var profile = Build(new FakeAsanaCredentialStore());

        Assert.False(profile.IsAsanaConnected);
        Assert.Equal("Nicht verbunden", profile.AsanaStatusText);
        Assert.Equal("Verbinden", profile.AsanaActionText);
    }

    [Fact]
    public void ConnectedStateShowsNameAndWorkspace()
    {
        var profile = Build(new FakeAsanaCredentialStore
        {
            Stored = new AsanaCredentials("t", "111", "Simon Strubel", "900", "ADVISORI"),
        });

        Assert.True(profile.IsAsanaConnected);
        Assert.Equal("Simon Strubel · ADVISORI", profile.AsanaStatusText);
        Assert.Equal("Trennen", profile.AsanaActionText);
    }

    [Fact]
    public void DisconnectClearsTheStoreAndAnnouncesTheChange()
    {
        var store = new FakeAsanaCredentialStore
        {
            Stored = new AsanaCredentials("t", "111", "Simon Strubel", "900", "ADVISORI"),
        };
        var profile = Build(store);
        var announced = 0;
        profile.AsanaConnectionChanged += () => announced++;

        profile.DisconnectAsanaCommand.Execute(null);

        Assert.Null(store.Stored);
        Assert.False(profile.IsAsanaConnected);
        Assert.Equal("Nicht verbunden", profile.AsanaStatusText);
        Assert.Equal(1, announced);
    }

    [Fact]
    public void ConnectRaisesTheRequestWithoutTouchingTheStore()
    {
        var store = new FakeAsanaCredentialStore();
        var profile = Build(store);
        var requested = 0;
        profile.AsanaConnectRequested += () => requested++;

        profile.ConnectAsanaCommand.Execute(null);

        Assert.Equal(1, requested);
        Assert.Null(store.Stored);
    }

    /// <summary>
    /// Der Zeitpunkt des letzten Asana-Abrufs steht getrennt neben dem von Jira: Beide Quellen
    /// werden unabhängig voneinander abgerufen, ein gemeinsamer Zeitstempel verwischte das.
    /// </summary>
    [Fact]
    public void AsanaLastSyncTextComesFromTheStoredMoment()
    {
        var settings = new FakeSettingsStore
        {
            Settings = new AppSettings
            {
                AsanaLastSyncAt = new DateTimeOffset(2026, 9, 17, 9, 12, 0, TimeSpan.FromHours(2)),
            },
        };

        var withMoment = new ProfileViewModel(
            settings,
            new JiraUser { AccountId = "1", DisplayName = "Simon Strubel", EmailAddress = "a@b.de" },
            "https://example.atlassian.net",
            WorkSchedule.Default(),
            themeSwitcher: null,
            asanaCredentialStore: new FakeAsanaCredentialStore());

        Assert.Equal("17.09.2026, 09:12", withMoment.AsanaLastSyncText);
        Assert.Equal("–", Build(new FakeAsanaCredentialStore()).AsanaLastSyncText);
    }
}
