using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.Core.Jira;
using Hora.Core.Storage;

namespace Hora.App.Tests;

public class MainViewModelTests
{
    private static readonly StoredCredentials ValidCredentials = new("https://example.atlassian.net", "a@b.de", "tok");

    private static (MainViewModel vm, FakeJiraClient client, FakeCredentialStore credentials) Create()
    {
        var client = new FakeJiraClient();
        var credentials = new FakeCredentialStore();
        var vm = new MainViewModel(credentials, new FakeSettingsStore(), new NullLogger(), (_, _, _) => client, () => new DateOnly(2026, 9, 16));
        return (vm, client, credentials);
    }

    [Fact]
    public async Task StartAsync_without_stored_credentials_shows_login()
    {
        var (vm, _, _) = Create();

        await vm.StartAsync();

        var login = Assert.IsType<LoginViewModel>(vm.CurrentScreen);
        Assert.Null(login.ErrorMessage);
        Assert.False(vm.IsStarting);
    }

    [Fact]
    public async Task StartAsync_with_valid_stored_credentials_shows_overview()
    {
        var (vm, _, credentials) = Create();
        credentials.Stored = ValidCredentials;

        await vm.StartAsync();

        var overview = Assert.IsType<OverviewViewModel>(vm.CurrentScreen);
        Assert.Equal("Simon", overview.UserDisplayName);
        Assert.False(vm.IsStarting);
    }

    [Fact]
    public async Task StartAsync_with_rejected_credentials_deletes_them_and_shows_login_with_hint()
    {
        var (vm, client, credentials) = Create();
        credentials.Stored = ValidCredentials;
        client.MyselfFailure = new JiraAuthenticationException();

        await vm.StartAsync();

        var login = Assert.IsType<LoginViewModel>(vm.CurrentScreen);
        Assert.Equal("Gespeicherte Anmeldung ist ungültig. Bitte erneut anmelden.", login.ErrorMessage);
        Assert.Equal(1, credentials.DeleteCalls);
        Assert.Null(credentials.Stored);
    }

    [Fact]
    public async Task StartAsync_when_jira_is_unreachable_shows_login_and_keeps_credentials()
    {
        var (vm, client, credentials) = Create();
        credentials.Stored = ValidCredentials;
        client.MyselfFailure = new JiraUnavailableException("offline");

        await vm.StartAsync();

        var login = Assert.IsType<LoginViewModel>(vm.CurrentScreen);
        Assert.Equal("Jira ist nicht erreichbar.", login.ErrorMessage);
        Assert.Equal(0, credentials.DeleteCalls);
        Assert.NotNull(credentials.Stored);
    }

    [Fact]
    public async Task StartAsync_when_loading_stored_credentials_throws_unexpectedly_shows_login_with_message()
    {
        var (vm, _, credentials) = Create();
        credentials.LoadFailure = new UnauthorizedAccessException("Zugriff auf credentials.dat verweigert");

        await vm.StartAsync();

        var login = Assert.IsType<LoginViewModel>(vm.CurrentScreen);
        Assert.Equal(
            "Es ist ein unerwarteter Fehler aufgetreten. Bitte erneut anmelden; Einzelheiten stehen im Protokoll.",
            login.ErrorMessage);
        Assert.False(vm.IsStarting);
    }

    [Fact]
    public async Task Successful_login_switches_to_overview()
    {
        var (vm, _, _) = Create();
        await vm.StartAsync();
        var login = Assert.IsType<LoginViewModel>(vm.CurrentScreen);
        login.BaseUrl = "https://example.atlassian.net";
        login.Email = "a@b.de";
        login.ApiToken = "tok";

        await login.LoginCommand.ExecuteAsync(null);

        Assert.IsType<OverviewViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public async Task Logout_returns_to_login_and_deletes_credentials()
    {
        var (vm, _, credentials) = Create();
        credentials.Stored = ValidCredentials;
        await vm.StartAsync();
        var overview = Assert.IsType<OverviewViewModel>(vm.CurrentScreen);

        overview.LogoutCommand.Execute(null);

        Assert.IsType<LoginViewModel>(vm.CurrentScreen);
        Assert.Equal(1, credentials.DeleteCalls);
    }
}
