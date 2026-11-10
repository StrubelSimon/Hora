using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.Core.Jira.Models;
using Hora.Core.Storage;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

public class ProfileAsanaTests
{
    private static ProfileViewModel Build(FakeEverhourCredentialStore asanaStore) =>
        Build(asanaStore, new FakeSettingsStore());

    private static ProfileViewModel Build(FakeEverhourCredentialStore asanaStore, FakeSettingsStore settings) => new(
        settings,
        new JiraUser { AccountId = "1", DisplayName = "Simon Strubel", EmailAddress = "a@b.de" },
        "https://example.atlassian.net",
        WorkSchedule.Default(),
        themeSwitcher: null,
        everhourCredentialStore: asanaStore);

    [Fact]
    public void WithoutConnectionItSaysSoAndOffersConnecting()
    {
        var profile = Build(new FakeEverhourCredentialStore());

        Assert.False(profile.IsAsanaConnected);
        Assert.Equal("Nicht verbunden", profile.AsanaStatusText);
        Assert.Equal("Verbinden", profile.AsanaActionText);
    }

    [Fact]
    public void ConnectedStateShowsNameAndSource()
    {
        var profile = Build(new FakeEverhourCredentialStore
        {
            Stored = new EverhourCredentials("k", 1431001, "Simon Strubel"),
        });

        Assert.True(profile.IsAsanaConnected);
        Assert.Equal("Simon Strubel · über Everhour", profile.AsanaStatusText);
        Assert.Equal("Trennen", profile.AsanaActionText);
    }

    [Fact]
    public void DisconnectClearsTheStoreAndAnnouncesTheChange()
    {
        var store = new FakeEverhourCredentialStore
        {
            Stored = new EverhourCredentials("k", 1431001, "Simon Strubel"),
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
        var store = new FakeEverhourCredentialStore();
        var profile = Build(store);
        var requested = 0;
        profile.AsanaConnectRequested += () => requested++;

        profile.ConnectAsanaCommand.Execute(null);

        Assert.Equal(1, requested);
        Assert.Null(store.Stored);
    }

    // ===================== Nur echte Änderungen werden gemeldet =====================

    private static EverhourCredentials Connection(string apiKey = "k", string userName = "Simon Strubel") =>
        new(apiKey, 1431001, userName);

    /// <summary>
    /// Am Ereignis hängt ein voller Neuabruf, der die Übersicht zwischendurch leer laufen lässt.
    /// Die Hülle ruft RefreshAsanaState nach jedem Schließen des Verbinden-Fensters, also auch nach
    /// einem Abbruch — dann hat sich am Speicher nichts geändert und es gibt nichts zu melden.
    /// </summary>
    [Fact]
    public void RefreshWithoutAChangeAnnouncesNothing()
    {
        var profile = Build(new FakeEverhourCredentialStore { Stored = Connection() });
        var announced = 0;
        profile.AsanaConnectionChanged += () => announced++;

        profile.RefreshAsanaState();

        Assert.Equal(0, announced);
        Assert.True(profile.IsAsanaConnected);
        Assert.Equal("Simon Strubel · über Everhour", profile.AsanaStatusText);
    }

    /// <summary>Dasselbe ohne jede Verbindung: Nichts da, nichts geändert, nichts zu melden.</summary>
    [Fact]
    public void RefreshWithoutAConnectionAnnouncesNothing()
    {
        var profile = Build(new FakeEverhourCredentialStore());
        var announced = 0;
        profile.AsanaConnectionChanged += () => announced++;

        profile.RefreshAsanaState();

        Assert.Equal(0, announced);
        Assert.False(profile.IsAsanaConnected);
    }

    /// <summary>Eine neu entstandene Verbindung wird gemeldet — das ist der Weg aus dem Fenster heraus.</summary>
    [Fact]
    public void ConnectingAnnouncesTheChange()
    {
        var store = new FakeEverhourCredentialStore();
        var profile = Build(store);
        var announced = 0;
        profile.AsanaConnectionChanged += () => announced++;

        store.Stored = Connection();
        profile.RefreshAsanaState();

        Assert.Equal(1, announced);
        Assert.True(profile.IsAsanaConnected);
        Assert.Equal("Simon Strubel · über Everhour", profile.AsanaStatusText);
        Assert.Equal("Trennen", profile.AsanaActionText);
    }

    /// <summary>
    /// Ein gewechselter Schlüssel bei durchgehend bestehender Verbindung ist ebenfalls eine
    /// Änderung: Dahinter stehen andere Zeiten, auch wenn „verbunden ja/nein“ gleich bleibt.
    /// </summary>
    [Fact]
    public void SwitchingTheApiKeyAnnouncesTheChange()
    {
        var store = new FakeEverhourCredentialStore { Stored = Connection() };
        var profile = Build(store);
        var announced = 0;
        profile.AsanaConnectionChanged += () => announced++;

        store.Stored = Connection("k2", "Simon S.");
        profile.RefreshAsanaState();

        Assert.Equal(1, announced);
        Assert.True(profile.IsAsanaConnected);
        Assert.Equal("Simon S. · über Everhour", profile.AsanaStatusText);
    }

    /// <summary>
    /// Wer aus der offenen Profilseite heraus verbindet, sieht die Zeile „Letzte Synchronisation“
    /// erscheinen. Sie darf dort nicht auf „–“ stehen bleiben, während der angestoßene Neuabruf den
    /// Zeitpunkt längst geschrieben hat.
    /// </summary>
    [Fact]
    public void AsanaLastSyncTextFollowsAMomentWrittenInTheMeantime()
    {
        var settings = new FakeSettingsStore();
        var profile = Build(new FakeEverhourCredentialStore(), settings);
        var changedProperties = new List<string>();
        profile.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName ?? string.Empty);

        Assert.Equal("–", profile.AsanaLastSyncText);

        // Der Abruf läuft und schreibt den Zeitpunkt, während die Seite offen steht.
        settings.Settings = new AppSettings
        {
            AsanaLastSyncAt = new DateTimeOffset(2026, 9, 17, 9, 12, 0, TimeSpan.FromHours(2)),
        };
        profile.RefreshAsanaState();

        Assert.Equal("17.09.2026, 09:12", profile.AsanaLastSyncText);
        Assert.Contains("AsanaLastSyncText", changedProperties);
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
            everhourCredentialStore: new FakeEverhourCredentialStore());

        Assert.Equal("17.09.2026, 09:12", withMoment.AsanaLastSyncText);
        Assert.Equal("–", Build(new FakeEverhourCredentialStore()).AsanaLastSyncText);
    }
}
