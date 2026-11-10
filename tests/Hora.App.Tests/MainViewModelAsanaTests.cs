using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.Core.Everhour;
using Hora.Core.Everhour.Models;
using Hora.Core.Storage;

namespace Hora.App.Tests;

public class MainViewModelAsanaTests
{
    private static MainViewModel Build(
        FakeEverhourCredentialStore asanaStore,
        out FakeCredentialStore jiraStore,
        out List<string> tokensUsed)
    {
        jiraStore = new FakeCredentialStore
        {
            Stored = new StoredCredentials("https://example.atlassian.net", "a@b.de", "jira-token"),
        };
        var usedTokens = new List<string>();
        tokensUsed = usedTokens;

        return new MainViewModel(
            jiraStore,
            new FakeSettingsStore(),
            new NullLogger(),
            (_, _, _) => new FakeJiraClient(),
            () => new DateOnly(2026, 9, 22),
            everhourCredentialStore: asanaStore,
            everhourClientFactory: token =>
            {
                usedTokens.Add(token);
                return new StubEverhourClient();
            });
    }

    private sealed class StubEverhourClient : IEverhourClient
    {
        public Task<EverhourUser> GetMyselfAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new EverhourUser { Id = 1431001 });

        public Task<IReadOnlyList<EverhourTimeEntry>> GetTimeEntriesAsync(
            long userId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EverhourTimeEntry>>(Array.Empty<EverhourTimeEntry>());

        public Task<EverhourProject> GetProjectAsync(string projectId, CancellationToken cancellationToken) =>
            Task.FromResult(new EverhourProject { Id = projectId, Name = "Projekt" });
    }

    [Fact]
    public async Task WithoutStoredConnectionNoAsanaClientIsBuilt()
    {
        var viewModel = Build(new FakeEverhourCredentialStore(), out _, out var tokensUsed);

        await viewModel.StartAsync();

        Assert.Empty(tokensUsed);
        Assert.IsType<OverviewViewModel>(viewModel.CurrentScreen);
    }

    [Fact]
    public async Task StoredConnectionBuildsTheClientWithTheSavedApiKey()
    {
        var asanaStore = new FakeEverhourCredentialStore
        {
            Stored = new EverhourCredentials("everhour-key", 1431001, "Simon"),
        };

        var viewModel = Build(asanaStore, out _, out var tokensUsed);
        await viewModel.StartAsync();

        Assert.Equal("everhour-key", Assert.Single(tokensUsed));
    }

    /// <summary>
    /// Abmelden räumt beide Verbindungen weg — Jira und Asana sind eine Identität. Bliebe asana.dat
    /// liegen, hinge nach dem nächsten Anmelden eine fremde Asana-Verbindung an der Sitzung.
    /// </summary>
    [Fact]
    public async Task LogoutDeletesTheAsanaConnectionToo()
    {
        var asanaStore = new FakeEverhourCredentialStore
        {
            Stored = new EverhourCredentials("everhour-key", 1431001, "Simon"),
        };
        var viewModel = Build(asanaStore, out var jiraStore, out _);
        await viewModel.StartAsync();

        var overview = Assert.IsType<OverviewViewModel>(viewModel.CurrentScreen);
        overview.LogoutCommand.Execute(null);

        Assert.Null(asanaStore.Stored);
        Assert.True(asanaStore.DeleteCount > 0);
        Assert.Null(jiraStore.Stored);
    }

    /// <summary>
    /// Fix-Runde 1 (I-1): Eine Anmeldung über das Formular ist eine frische Identitätsbehauptung —
    /// möglicherweise meldet sich jemand anderes an. Eine stehengebliebene Asana-Verbindung des
    /// Vorgängers muss deshalb fallen, sonst erbt die neue Jira-Identität sie stillschweigend.
    /// </summary>
    [Fact]
    public async Task LoginViaFormDeletesAnExistingAsanaConnection()
    {
        var asanaStore = new FakeEverhourCredentialStore
        {
            Stored = new EverhourCredentials("everhour-key", 1431001, "Simon"),
        };
        var viewModel = Build(asanaStore, out var jiraStore, out _);
        // Keine gespeicherte Jira-Sitzung: StartAsync zeigt das Anmeldeformular statt automatisch
        // anzumelden.
        jiraStore.Stored = null;
        await viewModel.StartAsync();
        var login = Assert.IsType<LoginViewModel>(viewModel.CurrentScreen);
        login.BaseUrl = "https://example.atlassian.net";
        login.Email = "a@b.de";
        login.ApiToken = "jira-token";

        await login.LoginCommand.ExecuteAsync(null);

        Assert.IsType<OverviewViewModel>(viewModel.CurrentScreen);
        Assert.Null(asanaStore.Stored);
        Assert.True(asanaStore.DeleteCount > 0);
    }

    /// <summary>
    /// Fix-Runde 1 (I-1), der wichtigere der beiden Tests: Die automatische Anmeldung in StartAsync
    /// mit den gespeicherten Zugangsdaten setzt dieselbe Identität fort. Anders als die Anmeldung
    /// über das Formular darf sie eine vorhandene Asana-Verbindung nicht anrühren.
    /// </summary>
    [Fact]
    public async Task AutomaticLoginAtStartupLeavesAnExistingAsanaConnectionUntouched()
    {
        var asanaStore = new FakeEverhourCredentialStore
        {
            Stored = new EverhourCredentials("everhour-key", 1431001, "Simon"),
        };
        var viewModel = Build(asanaStore, out _, out var tokensUsed);

        await viewModel.StartAsync();

        Assert.IsType<OverviewViewModel>(viewModel.CurrentScreen);
        Assert.NotNull(asanaStore.Stored);
        Assert.Equal(0, asanaStore.DeleteCount);
        Assert.Equal("everhour-key", Assert.Single(tokensUsed));
    }

    // ===================== Zeitpunkt des letzten Asana-Abrufs =====================

    private static readonly DateTimeOffset Now = new(2026, 9, 22, 8, 30, 0, TimeSpan.FromHours(2));

    private static MainViewModel BuildWith(FakeSettingsStore settings, FakeEverhourCredentialStore asanaStore) =>
        new(
            new FakeCredentialStore
            {
                Stored = new StoredCredentials("https://example.atlassian.net", "a@b.de", "jira-token"),
            },
            settings,
            new NullLogger(),
            (_, _, _) => new FakeJiraClient(),
            () => new DateOnly(2026, 9, 22),
            now: () => Now,
            everhourCredentialStore: asanaStore,
            everhourClientFactory: _ => new StubEverhourClient());

    /// <summary>
    /// Der Zeitpunkt des letzten Abrufs wird für Asana getrennt festgehalten: Die Profilseite zeigt
    /// ihn in der eigenen Asana-Zeile, nicht in der von Jira.
    /// </summary>
    [Fact]
    public async Task ASuccessfulFetchStoresTheAsanaMomentWhileConnected()
    {
        var settings = new FakeSettingsStore();
        var viewModel = BuildWith(
            settings,
            new FakeEverhourCredentialStore
            {
                Stored = new EverhourCredentials("everhour-key", 1431001, "Simon"),
            });

        await viewModel.StartAsync();

        Assert.Equal(Now, settings.Settings.LastSyncAt);
        Assert.Equal(Now, settings.Settings.AsanaLastSyncAt);
    }

    /// <summary>
    /// Ohne Verbindung stünde dort ein Zeitpunkt, zu dem nie etwas von Asana geholt wurde — die
    /// Zeile bleibt deshalb leer.
    /// </summary>
    [Fact]
    public async Task ASuccessfulFetchLeavesTheAsanaMomentEmptyWithoutAConnection()
    {
        var settings = new FakeSettingsStore();
        var viewModel = BuildWith(settings, new FakeEverhourCredentialStore());

        await viewModel.StartAsync();

        Assert.Equal(Now, settings.Settings.LastSyncAt);
        Assert.Null(settings.Settings.AsanaLastSyncAt);
    }

    // ===================== Neuladen nach einem Wechsel der Verbindung =====================

    private static MainViewModel BuildCounting(FakeEverhourCredentialStore asanaStore, out FakeJiraClient client)
    {
        var jiraClient = new FakeJiraClient();
        client = jiraClient;

        return new MainViewModel(
            new FakeCredentialStore
            {
                Stored = new StoredCredentials("https://example.atlassian.net", "a@b.de", "jira-token"),
            },
            new FakeSettingsStore(),
            new NullLogger(),
            (_, _, _) => jiraClient,
            () => new DateOnly(2026, 9, 22),
            everhourCredentialStore: asanaStore,
            everhourClientFactory: _ => new StubEverhourClient());
    }

    private static ProfileViewModel OpenProfile(MainViewModel viewModel)
    {
        var overview = Assert.IsType<OverviewViewModel>(viewModel.CurrentScreen);
        overview.ShowProfileCommand.Execute(null);
        return Assert.IsType<ProfileViewModel>(viewModel.CurrentScreen);
    }

    /// <summary>
    /// Der Startweg holt genau einmal. ApplyAsanaConnection läuft dort mit, darf aber nichts
    /// nachladen: RunInitialLoadAsync stößt den ersten Abruf bereits an. Läge das Neuladen in
    /// ApplyAsanaConnection statt am Wechsel der Verbindung, stünde hier eine Zwei — zwei volle
    /// Abrufe bei jedem Start, gegen dieselbe Jira-Instanz. Dieser Test hält die Unterscheidung fest.
    /// </summary>
    [Fact]
    public async Task TheStartupPathFetchesExactlyOnce()
    {
        var viewModel = BuildCounting(
            new FakeEverhourCredentialStore
            {
                Stored = new EverhourCredentials("everhour-key", 1431001, "Simon"),
            },
            out var client);

        await viewModel.StartAsync();

        Assert.Equal(1, client.SearchCalls);
    }

    /// <summary>
    /// Nach dem Verbinden erscheinen die Asana-Zeiten sofort und nicht erst beim nächsten Takt.
    /// Der Steckplatz allein genügt dafür nicht: Die Übersicht steht mit dem Stand da, den sie vor
    /// dem Wechsel geholt hat.
    /// </summary>
    [Fact]
    public async Task ConnectingAsanaFetchesAgainSoTheTimesShowUpAtOnce()
    {
        var asanaStore = new FakeEverhourCredentialStore();
        var viewModel = BuildCounting(asanaStore, out var client);
        await viewModel.StartAsync();

        var profile = OpenProfile(viewModel);
        var callsBefore = client.SearchCalls;

        // Genau dies tut das Verbinden-Fenster: Es speichert die Verbindung, und die Hülle lässt
        // die Profilseite den Stand danach neu lesen.
        asanaStore.Stored = new EverhourCredentials("everhour-key", 1431001, "Simon");
        profile.RefreshAsanaState();

        Assert.Equal(callsBefore + 1, client.SearchCalls);
    }

    /// <summary>
    /// Der wichtigste Test dieser Runde: Wer das Verbinden-Fenster öffnet und abbricht, darf die
    /// Übersicht nicht kosten. Die Hülle ruft RefreshAsanaState nach jedem ShowDialog — auch nach
    /// einem Abbruch —, und am Ereignis hängt ein erzwungener Neuabruf, der den angezeigten
    /// Zeitraum vorher verwirft. Ohne den Vergleich im ViewModel liefe die Seite hier leer und lüde
    /// neu, für nichts.
    /// </summary>
    [Fact]
    public async Task CancellingTheConnectWindowFetchesNothingAgain()
    {
        var asanaStore = new FakeEverhourCredentialStore
        {
            Stored = new EverhourCredentials("everhour-key", 1431001, "Simon"),
        };
        var viewModel = BuildCounting(asanaStore, out var client);
        await viewModel.StartAsync();

        var profile = OpenProfile(viewModel);
        var callsBefore = client.SearchCalls;

        // Genau das tut die Hülle nach einem abgebrochenen Fenster: neu lesen, ohne dass sich am
        // Speicher etwas geändert hätte.
        profile.RefreshAsanaState();

        Assert.Equal(callsBefore, client.SearchCalls);
    }

    /// <summary>
    /// Ein gewechselter Schlüssel bei durchgehend bestehender Verbindung ist ebenfalls eine
    /// Änderung: Dahinter stehen andere Zeiten, auch wenn „verbunden ja/nein" gleich bleibt.
    /// </summary>
    [Fact]
    public async Task SwitchingTheApiKeyFetchesAgain()
    {
        var asanaStore = new FakeEverhourCredentialStore
        {
            Stored = new EverhourCredentials("everhour-key", 1431001, "Simon"),
        };
        var viewModel = BuildCounting(asanaStore, out var client);
        await viewModel.StartAsync();

        var profile = OpenProfile(viewModel);
        var callsBefore = client.SearchCalls;

        asanaStore.Stored = new EverhourCredentials("everhour-key-2", 1431001, "Simon");
        profile.RefreshAsanaState();

        Assert.Equal(callsBefore + 1, client.SearchCalls);
    }

    /// <summary>Und nach dem Trennen verschwinden sie ebenso sofort, statt stehen zu bleiben.</summary>
    [Fact]
    public async Task DisconnectingAsanaFetchesAgainSoTheTimesDisappearAtOnce()
    {
        var asanaStore = new FakeEverhourCredentialStore
        {
            Stored = new EverhourCredentials("everhour-key", 1431001, "Simon"),
        };
        var viewModel = BuildCounting(asanaStore, out var client);
        await viewModel.StartAsync();

        var profile = OpenProfile(viewModel);
        var callsBefore = client.SearchCalls;

        profile.DisconnectAsanaCommand.Execute(null);

        Assert.Null(asanaStore.Stored);
        Assert.Equal(callsBefore + 1, client.SearchCalls);
    }
}
