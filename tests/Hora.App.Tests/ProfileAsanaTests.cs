using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.Core.Jira.Models;
using Hora.Core.Storage;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

public class ProfileAsanaTests
{
    private static ProfileViewModel Build(FakeAsanaCredentialStore asanaStore) =>
        Build(asanaStore, new FakeSettingsStore());

    private static ProfileViewModel Build(FakeAsanaCredentialStore asanaStore, FakeSettingsStore settings) => new(
        settings,
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

    // ===================== Nur echte Änderungen werden gemeldet =====================

    private static AsanaCredentials Connection(string workspaceGid = "900", string workspaceName = "ADVISORI") =>
        new("t", "111", "Simon Strubel", workspaceGid, workspaceName);

    /// <summary>
    /// Am Ereignis hängt ein voller Neuabruf, der die Übersicht zwischendurch leer laufen lässt.
    /// Die Hülle ruft RefreshAsanaState nach jedem Schließen des Verbinden-Fensters, also auch nach
    /// einem Abbruch — dann hat sich am Speicher nichts geändert und es gibt nichts zu melden.
    /// </summary>
    [Fact]
    public void RefreshWithoutAChangeAnnouncesNothing()
    {
        var profile = Build(new FakeAsanaCredentialStore { Stored = Connection() });
        var announced = 0;
        profile.AsanaConnectionChanged += () => announced++;

        profile.RefreshAsanaState();

        Assert.Equal(0, announced);
        Assert.True(profile.IsAsanaConnected);
        Assert.Equal("Simon Strubel · ADVISORI", profile.AsanaStatusText);
    }

    /// <summary>Dasselbe ohne jede Verbindung: Nichts da, nichts geändert, nichts zu melden.</summary>
    [Fact]
    public void RefreshWithoutAConnectionAnnouncesNothing()
    {
        var profile = Build(new FakeAsanaCredentialStore());
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
        var store = new FakeAsanaCredentialStore();
        var profile = Build(store);
        var announced = 0;
        profile.AsanaConnectionChanged += () => announced++;

        store.Stored = Connection();
        profile.RefreshAsanaState();

        Assert.Equal(1, announced);
        Assert.True(profile.IsAsanaConnected);
        Assert.Equal("Simon Strubel · ADVISORI", profile.AsanaStatusText);
        Assert.Equal("Trennen", profile.AsanaActionText);
    }

    /// <summary>
    /// Ein gewechselter Workspace bei durchgehend bestehender Verbindung ist ebenfalls eine
    /// Änderung: Dahinter stehen andere Zeiten, auch wenn „verbunden ja/nein“ gleich bleibt.
    /// </summary>
    [Fact]
    public void SwitchingTheWorkspaceAnnouncesTheChange()
    {
        var store = new FakeAsanaCredentialStore { Stored = Connection() };
        var profile = Build(store);
        var announced = 0;
        profile.AsanaConnectionChanged += () => announced++;

        store.Stored = Connection("901", "Nebenprojekt");
        profile.RefreshAsanaState();

        Assert.Equal(1, announced);
        Assert.True(profile.IsAsanaConnected);
        Assert.Equal("Simon Strubel · Nebenprojekt", profile.AsanaStatusText);
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
        var profile = Build(new FakeAsanaCredentialStore(), settings);
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
            asanaCredentialStore: new FakeAsanaCredentialStore());

        Assert.Equal("17.09.2026, 09:12", withMoment.AsanaLastSyncText);
        Assert.Equal("–", Build(new FakeAsanaCredentialStore()).AsanaLastSyncText);
    }
}
