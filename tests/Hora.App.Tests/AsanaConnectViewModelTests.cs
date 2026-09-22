using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.Core.Asana;
using Hora.Core.Asana.Models;

namespace Hora.App.Tests;

public class AsanaConnectViewModelTests
{
    private sealed class StubAsanaClient : IAsanaClient
    {
        private readonly AsanaUser? _user;
        private readonly Exception? _throw;

        public StubAsanaClient(AsanaUser user)
        {
            _user = user;
        }

        public StubAsanaClient(Exception toThrow)
        {
            _throw = toThrow;
        }

        public Task<AsanaUser> GetMyselfAsync(CancellationToken cancellationToken) =>
            _throw is not null ? throw _throw : Task.FromResult(_user!);

        public Task<IReadOnlyList<AsanaTimeEntry>> GetTimeEntriesAsync(
            string userGid, string workspaceGid, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AsanaTimeEntry>>(Array.Empty<AsanaTimeEntry>());
    }

    private static AsanaUser User(params string[] workspaceNames) => new()
    {
        Gid = "111",
        Name = "Simon Strubel",
        Email = "simon.strubel@advisori.de",
        Workspaces = workspaceNames
            .Select((name, index) => new AsanaWorkspace { Gid = $"90{index}", Name = name })
            .ToList(),
    };

    private static AsanaConnectViewModel Build(IAsanaClient client, out FakeAsanaCredentialStore store)
    {
        var created = new FakeAsanaCredentialStore();
        store = created;
        return new AsanaConnectViewModel(_ => client, created);
    }

    [Fact]
    public void StartsOnTheTokenStepWithTheConnectButtonUnavailable()
    {
        var viewModel = Build(new StubAsanaClient(User("ADVISORI")), out _);

        Assert.True(viewModel.IsTokenStep);
        Assert.False(viewModel.IsWorkspaceStep);
        Assert.False(viewModel.ContinueCommand.CanExecute(null));
    }

    [Fact]
    public async Task ContinueFetchesIdentityAndMovesToTheWorkspaceStep()
    {
        var viewModel = Build(new StubAsanaClient(User("ADVISORI", "Privat")), out _);
        viewModel.Token = "asana-token";

        await viewModel.ContinueCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsWorkspaceStep);
        Assert.Equal("Simon Strubel", viewModel.UserName);
        Assert.Equal("simon.strubel@advisori.de", viewModel.UserEmail);
        Assert.Equal(2, viewModel.Workspaces.Count);
    }

    /// <summary>Gibt es nur einen Workspace, ist die Wahl keine — er steht vorausgewählt da.</summary>
    [Fact]
    public async Task SingleWorkspaceIsPreselected()
    {
        var viewModel = Build(new StubAsanaClient(User("ADVISORI")), out _);
        viewModel.Token = "asana-token";

        await viewModel.ContinueCommand.ExecuteAsync(null);

        Assert.Equal("ADVISORI", viewModel.SelectedWorkspace!.Name);
        Assert.True(viewModel.ConnectCommand.CanExecute(null));
    }

    [Fact]
    public async Task ConnectStoresEveryFieldAndRaisesTheEvent()
    {
        var viewModel = Build(new StubAsanaClient(User("ADVISORI")), out var store);
        viewModel.Token = "asana-token";
        await viewModel.ContinueCommand.ExecuteAsync(null);
        var raised = 0;
        viewModel.Connected += () => raised++;

        viewModel.ConnectCommand.Execute(null);

        Assert.Equal(1, raised);
        Assert.NotNull(store.Stored);
        Assert.Equal("asana-token", store.Stored!.Token);
        Assert.Equal("111", store.Stored.UserGid);
        Assert.Equal("Simon Strubel", store.Stored.UserName);
        Assert.Equal("900", store.Stored.WorkspaceGid);
        Assert.Equal("ADVISORI", store.Stored.WorkspaceName);
    }

    [Fact]
    public async Task RejectedTokenStaysOnTheTokenStepWithAMessage()
    {
        var viewModel = Build(new StubAsanaClient(new AsanaAuthenticationException()), out var store);
        viewModel.Token = "falsch";

        await viewModel.ContinueCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsTokenStep);
        Assert.NotNull(viewModel.ErrorMessage);
        Assert.Null(store.Stored);
    }

    [Fact]
    public async Task UnreachableAsanaStaysOnTheTokenStepWithAMessage()
    {
        var viewModel = Build(new StubAsanaClient(new AsanaUnavailableException("weg")), out _);
        viewModel.Token = "asana-token";

        await viewModel.ContinueCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsTokenStep);
        Assert.NotNull(viewModel.ErrorMessage);
    }

    /// <summary>Ein Konto ohne Workspace kann nichts liefern; das muss vor dem Speichern auffallen.</summary>
    [Fact]
    public async Task AccountWithoutWorkspacesIsRejected()
    {
        var viewModel = Build(new StubAsanaClient(User()), out var store);
        viewModel.Token = "asana-token";

        await viewModel.ContinueCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsTokenStep);
        Assert.NotNull(viewModel.ErrorMessage);
        Assert.Null(store.Stored);
    }

    [Fact]
    public async Task BackReturnsToTheTokenStepAndKeepsTheTypedToken()
    {
        var viewModel = Build(new StubAsanaClient(User("ADVISORI")), out _);
        viewModel.Token = "asana-token";
        await viewModel.ContinueCommand.ExecuteAsync(null);

        viewModel.BackCommand.Execute(null);

        Assert.True(viewModel.IsTokenStep);
        Assert.Equal("asana-token", viewModel.Token);
    }

    // ===================== Fix-Runde 1 (I-1): geprüfter Token und gespeicherter Token =====================

    /// <summary>
    /// Der Weg, den die Review gefunden hat: Token A prüfen, zurück, Token B eintippen. Die
    /// geprüfte Identität gehört zu A — sie muss mit der Eingabe fallen, sonst ließe sich B mit
    /// Kennung und Workspace von A speichern.
    /// </summary>
    [Fact]
    public async Task ChangingTheTokenAfterGoingBackWithdrawsTheVerifiedIdentity()
    {
        var viewModel = Build(new StubAsanaClient(User("ADVISORI")), out var store);
        viewModel.Token = "asana-token";
        await viewModel.ContinueCommand.ExecuteAsync(null);
        viewModel.BackCommand.Execute(null);

        viewModel.Token = "  never-verified  ";

        Assert.False(viewModel.ConnectCommand.CanExecute(null));
        Assert.Null(store.Stored);
    }

    /// <summary>
    /// CanExecute allein genügt nicht: Execute fragt es von sich aus nicht ab. Auch der
    /// unmittelbare Aufruf darf nichts speichern, solange die Identität nicht zum eingegebenen
    /// Token gehört.
    /// </summary>
    [Fact]
    public async Task AChangedTokenStoresNothingEvenWhenConnectIsExecutedDirectly()
    {
        var viewModel = Build(new StubAsanaClient(User("ADVISORI")), out var store);
        viewModel.Token = "asana-token";
        await viewModel.ContinueCommand.ExecuteAsync(null);
        viewModel.BackCommand.Execute(null);
        viewModel.Token = "never-verified";

        viewModel.ConnectCommand.Execute(null);

        Assert.Null(store.Stored);
    }

    /// <summary>
    /// Der geordnete zweite Anlauf: Wer zurückgeht, einen anderen Token eintippt und erneut prüfen
    /// lässt, speichert Token und Identität als ein Stück — niemals Token B mit der Kennung von A.
    /// </summary>
    [Fact]
    public async Task AVerifiedSecondTokenIsStoredWithItsOwnIdentity()
    {
        var store = new FakeAsanaCredentialStore();
        var first = new StubAsanaClient(new AsanaUser
        {
            Gid = "111",
            Name = "Simon Strubel",
            Email = "simon.strubel@advisori.de",
            Workspaces = new List<AsanaWorkspace> { new() { Gid = "900", Name = "ADVISORI" } },
        });
        var second = new StubAsanaClient(new AsanaUser
        {
            Gid = "222",
            Name = "Zweites Konto",
            Email = "zweites@example.de",
            Workspaces = new List<AsanaWorkspace> { new() { Gid = "901", Name = "Privat" } },
        });
        var viewModel = new AsanaConnectViewModel(token => token == "token-b" ? second : first, store);

        viewModel.Token = "token-a";
        await viewModel.ContinueCommand.ExecuteAsync(null);
        viewModel.BackCommand.Execute(null);
        viewModel.Token = "token-b";
        await viewModel.ContinueCommand.ExecuteAsync(null);
        viewModel.ConnectCommand.Execute(null);

        Assert.NotNull(store.Stored);
        Assert.Equal("token-b", store.Stored!.Token);
        Assert.Equal("222", store.Stored.UserGid);
        Assert.Equal("Zweites Konto", store.Stored.UserName);
        Assert.Equal("901", store.Stored.WorkspaceGid);
        Assert.Equal("Privat", store.Stored.WorkspaceName);
    }
}
