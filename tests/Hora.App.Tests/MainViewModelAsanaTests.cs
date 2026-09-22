using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.Core.Asana;
using Hora.Core.Storage;

namespace Hora.App.Tests;

public class MainViewModelAsanaTests
{
    private static MainViewModel Build(
        FakeAsanaCredentialStore asanaStore,
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
            asanaCredentialStore: asanaStore,
            asanaClientFactory: token =>
            {
                usedTokens.Add(token);
                return new StubAsanaClient();
            });
    }

    private sealed class StubAsanaClient : IAsanaClient
    {
        public Task<Hora.Core.Asana.Models.AsanaUser> GetMyselfAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new Hora.Core.Asana.Models.AsanaUser { Gid = "111" });

        public Task<IReadOnlyList<Hora.Core.Asana.Models.AsanaTimeEntry>> GetTimeEntriesAsync(
            string userGid, string workspaceGid, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Hora.Core.Asana.Models.AsanaTimeEntry>>(
                Array.Empty<Hora.Core.Asana.Models.AsanaTimeEntry>());
    }

    [Fact]
    public async Task WithoutStoredConnectionNoAsanaClientIsBuilt()
    {
        var viewModel = Build(new FakeAsanaCredentialStore(), out _, out var tokensUsed);

        await viewModel.StartAsync();

        Assert.Empty(tokensUsed);
        Assert.IsType<OverviewViewModel>(viewModel.CurrentScreen);
    }

    [Fact]
    public async Task StoredConnectionBuildsTheClientWithTheSavedToken()
    {
        var asanaStore = new FakeAsanaCredentialStore
        {
            Stored = new AsanaCredentials("asana-token", "111", "Simon", "900", "ADVISORI"),
        };

        var viewModel = Build(asanaStore, out _, out var tokensUsed);
        await viewModel.StartAsync();

        Assert.Equal("asana-token", Assert.Single(tokensUsed));
    }

    /// <summary>
    /// Abmelden räumt beide Verbindungen weg — Jira und Asana sind eine Identität. Bliebe asana.dat
    /// liegen, hinge nach dem nächsten Anmelden eine fremde Asana-Verbindung an der Sitzung.
    /// </summary>
    [Fact]
    public async Task LogoutDeletesTheAsanaConnectionToo()
    {
        var asanaStore = new FakeAsanaCredentialStore
        {
            Stored = new AsanaCredentials("asana-token", "111", "Simon", "900", "ADVISORI"),
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
        var asanaStore = new FakeAsanaCredentialStore
        {
            Stored = new AsanaCredentials("asana-token", "111", "Simon", "900", "ADVISORI"),
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
        var asanaStore = new FakeAsanaCredentialStore
        {
            Stored = new AsanaCredentials("asana-token", "111", "Simon", "900", "ADVISORI"),
        };
        var viewModel = Build(asanaStore, out _, out var tokensUsed);

        await viewModel.StartAsync();

        Assert.IsType<OverviewViewModel>(viewModel.CurrentScreen);
        Assert.NotNull(asanaStore.Stored);
        Assert.Equal(0, asanaStore.DeleteCount);
        Assert.Equal("asana-token", Assert.Single(tokensUsed));
    }
}
