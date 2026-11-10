using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.Core.Everhour;
using Hora.Core.Everhour.Models;
using Hora.Core.Storage;

namespace Hora.App.Tests;

public class EverhourConnectViewModelTests
{
    private sealed class StubEverhourClient : IEverhourClient
    {
        private readonly EverhourUser? _user;
        private readonly Exception? _throw;

        public StubEverhourClient(EverhourUser user)
        {
            _user = user;
        }

        public StubEverhourClient(Exception toThrow)
        {
            _throw = toThrow;
        }

        public Task<EverhourUser> GetMyselfAsync(CancellationToken cancellationToken) =>
            _throw is not null ? throw _throw : Task.FromResult(_user!);

        public Task<IReadOnlyList<EverhourTimeEntry>> GetTimeEntriesAsync(
            long userId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EverhourTimeEntry>>(Array.Empty<EverhourTimeEntry>());

        public Task<EverhourProject> GetProjectAsync(string projectId, CancellationToken cancellationToken) =>
            Task.FromResult(new EverhourProject { Id = projectId });
    }

    private static readonly EverhourUser Simon = new() { Id = 1431001, Name = "Simon Strubel" };

    private static EverhourConnectViewModel Build(
        IEverhourClient client, out FakeEverhourCredentialStore store, List<string>? keysUsed = null)
    {
        var created = new FakeEverhourCredentialStore();
        store = created;
        return new EverhourConnectViewModel(
            key =>
            {
                keysUsed?.Add(key);
                return client;
            },
            created);
    }

    [Fact]
    public void ConnectIsUnavailableWithoutAKey()
    {
        var viewModel = Build(new StubEverhourClient(Simon), out _);

        Assert.False(viewModel.ConnectCommand.CanExecute(null));

        viewModel.ApiKey = "   ";
        Assert.False(viewModel.ConnectCommand.CanExecute(null));

        viewModel.ApiKey = "eh-key";
        Assert.True(viewModel.ConnectCommand.CanExecute(null));
    }

    /// <summary>
    /// Gespeichert wird der geprüfte Schlüssel ohne Leerraum aus dem Einfügen, dazu Kennung und
    /// Name aus Everhours Antwort — nicht etwas Eingetipptes.
    /// </summary>
    [Fact]
    public async Task ConnectVerifiesTheKeyStoresItAndRaisesTheEvent()
    {
        var keysUsed = new List<string>();
        var viewModel = Build(new StubEverhourClient(Simon), out var store, keysUsed);
        var raised = 0;
        viewModel.Connected += () => raised++;

        viewModel.ApiKey = "  eh-key \n";
        await viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.Equal("eh-key", Assert.Single(keysUsed));
        Assert.Equal(new EverhourCredentials("eh-key", 1431001, "Simon Strubel"), store.Stored);
        Assert.Equal(1, raised);
        Assert.False(viewModel.HasError);
        Assert.False(viewModel.IsBusy);
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task FailureStoresNothingAndShowsAMessage(Exception failure, string expectedMessage)
    {
        var viewModel = Build(new StubEverhourClient(failure), out var store);
        var raised = 0;
        viewModel.Connected += () => raised++;

        viewModel.ApiKey = "eh-key";
        await viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.Null(store.Stored);
        Assert.Equal(0, raised);
        Assert.Equal(expectedMessage, viewModel.ErrorMessage);
        Assert.False(viewModel.IsBusy);
    }

    public static TheoryData<Exception, string> Failures() => new()
    {
        { new EverhourAuthenticationException(), "Everhour hat den Schlüssel abgelehnt. Bitte prüfen und erneut versuchen." },
        { new EverhourUnavailableException("weg"), "Everhour ist nicht erreichbar. Netzwerkverbindung prüfen." },
        { new EverhourRateLimitException(), "Die Verbindung zu Everhour ist fehlgeschlagen." },
        { new InvalidOperationException(), "Es ist ein unerwarteter Fehler aufgetreten. Bitte erneut versuchen." },
    };

    /// <summary>Ohne Nutzerkennung ließen sich keine Zeiten abfragen; eine solche Verbindung wäre wertlos.</summary>
    [Fact]
    public async Task AnswerWithoutUserIdIsRejected()
    {
        var viewModel = Build(new StubEverhourClient(new EverhourUser { Id = 0, Name = "?" }), out var store);

        viewModel.ApiKey = "eh-key";
        await viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.Null(store.Stored);
        Assert.True(viewModel.HasError);
    }

    [Fact]
    public async Task ASecondAttemptClearsTheOldMessage()
    {
        var viewModel = Build(new StubEverhourClient(Simon), out _);
        viewModel.ErrorMessage = "alt";

        viewModel.ApiKey = "eh-key";
        await viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public void TheButtonNamesWhatIsHappening()
    {
        var viewModel = Build(new StubEverhourClient(Simon), out _);
        Assert.Equal("Verbinden", viewModel.ConnectButtonText);

        viewModel.IsBusy = true;
        Assert.Equal("Schlüssel wird geprüft …", viewModel.ConnectButtonText);
        Assert.False(viewModel.ConnectCommand.CanExecute(null));
    }
}
