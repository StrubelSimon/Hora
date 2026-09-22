using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.Core.Jira;
using Hora.Core.Jira.Models;
using Hora.Core.Storage;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

public class LoginViewModelTests
{
    private sealed class Harness
    {
        public FakeJiraClient Client { get; } = new();
        public FakeCredentialStore Credentials { get; } = new();
        public FakeSettingsStore Settings { get; } = new();
        public List<(string BaseUrl, string Email, string Token)> FactoryCalls { get; } = new();
        public LoginViewModel ViewModel { get; }
        public (IJiraClient Client, JiraUser User, string BaseUrl)? LoggedIn { get; private set; }

        public Harness(AppSettings? initialSettings = null)
        {
            ViewModel = new LoginViewModel(
                initialSettings ?? new AppSettings(),
                (baseUrl, email, token) =>
                {
                    FactoryCalls.Add((baseUrl, email, token));
                    return Client;
                },
                Settings,
                Credentials,
                new NullLogger());
            ViewModel.LoggedIn += (client, user, baseUrl) => LoggedIn = (client, user, baseUrl);
        }

        public Harness FillValidForm()
        {
            ViewModel.BaseUrl = "https://example.atlassian.net/";
            ViewModel.Email = " a@b.de ";
            ViewModel.ApiToken = "tok";
            return this;
        }
    }

    [Fact]
    public void Form_is_prefilled_from_settings()
    {
        var harness = new Harness(new AppSettings { BaseUrl = "https://other.atlassian.net", Email = "x@y.de" });

        Assert.Equal("https://other.atlassian.net", harness.ViewModel.BaseUrl);
        Assert.Equal("x@y.de", harness.ViewModel.Email);
        Assert.True(harness.ViewModel.RememberMe);
        Assert.Equal("Anmelden", harness.ViewModel.LoginButtonText);
    }

    [Fact]
    public void Login_is_disabled_until_all_fields_are_filled()
    {
        var harness = new Harness();
        var vm = harness.ViewModel;

        Assert.False(vm.LoginCommand.CanExecute(null));

        vm.Email = "a@b.de";
        Assert.False(vm.LoginCommand.CanExecute(null));

        vm.ApiToken = "tok";
        Assert.True(vm.LoginCommand.CanExecute(null));
    }

    [Fact]
    public async Task Login_rejects_url_without_https()
    {
        var harness = new Harness().FillValidForm();
        harness.ViewModel.BaseUrl = "http://example.atlassian.net";

        await harness.ViewModel.LoginCommand.ExecuteAsync(null);

        Assert.Equal("Die Jira-URL muss mit https:// beginnen.", harness.ViewModel.ErrorMessage);
        Assert.True(harness.ViewModel.HasError);
        Assert.Empty(harness.FactoryCalls);
    }

    [Fact]
    public async Task Successful_login_trims_input_saves_settings_and_credentials_and_raises_LoggedIn()
    {
        var harness = new Harness().FillValidForm();

        await harness.ViewModel.LoginCommand.ExecuteAsync(null);

        var factoryCall = Assert.Single(harness.FactoryCalls);
        Assert.Equal(("https://example.atlassian.net", "a@b.de", "tok"), factoryCall);
        Assert.Equal("https://example.atlassian.net", harness.Settings.Settings.BaseUrl);
        Assert.Equal("a@b.de", harness.Settings.Settings.Email);
        Assert.Equal(new StoredCredentials("https://example.atlassian.net", "a@b.de", "tok"), harness.Credentials.Stored);
        Assert.NotNull(harness.LoggedIn);
        Assert.Equal("Simon", harness.LoggedIn!.Value.User.DisplayName);
        Assert.Equal("https://example.atlassian.net", harness.LoggedIn.Value.BaseUrl);
        Assert.Null(harness.ViewModel.ErrorMessage);
        Assert.False(harness.ViewModel.IsBusy);
    }

    [Fact]
    public async Task Successful_login_keeps_schedule_and_theme_of_the_existing_settings()
    {
        // Befund A: Die Anmeldung schrieb ein frisches AppSettings und setzte damit die von Hand
        // eingetragenen Sollstunden auf acht Stunden Montag bis Freitag und das Design auf hell
        // zurück. settings.json ist die einzige Stelle, an der sich die Sollstunden einstellen
        // lassen — eine Teilzeitkraft verlor ihre sechs Stunden bei jeder erneuten Anmeldung.
        var harness = new Harness();
        harness.Settings.Settings = new AppSettings
        {
            BaseUrl = "https://alt.atlassian.net",
            Email = "alt@b.de",
            Schedule = new WorkSchedule { Monday = 6, Tuesday = 6, Wednesday = 6, Thursday = 6, Friday = 6 },
            Theme = "Dark",
        };
        harness.FillValidForm();

        await harness.ViewModel.LoginCommand.ExecuteAsync(null);

        var saved = harness.Settings.Settings;
        Assert.Equal(1, harness.Settings.SaveCalls);
        Assert.Equal("https://example.atlassian.net", saved.BaseUrl);
        Assert.Equal("a@b.de", saved.Email);
        Assert.Equal(6, saved.Schedule.Monday);
        Assert.Equal(6, saved.Schedule.Friday);
        Assert.Equal(0, saved.Schedule.Saturday);
        Assert.Equal("Dark", saved.Theme);
    }

    [Fact]
    public async Task Successful_login_without_remember_me_deletes_stored_credentials()
    {
        var harness = new Harness().FillValidForm();
        harness.Credentials.Stored = new StoredCredentials("https://alt.atlassian.net", "alt@b.de", "alt");
        harness.ViewModel.RememberMe = false;

        await harness.ViewModel.LoginCommand.ExecuteAsync(null);

        Assert.Null(harness.Credentials.Stored);
        Assert.Equal(1, harness.Credentials.DeleteCalls);
        Assert.Equal(1, harness.Settings.SaveCalls);
        Assert.NotNull(harness.LoggedIn);
    }

    [Fact]
    public async Task Wrong_credentials_show_message_and_do_not_save()
    {
        var harness = new Harness().FillValidForm();
        harness.Client.MyselfFailure = new JiraAuthenticationException();

        await harness.ViewModel.LoginCommand.ExecuteAsync(null);

        Assert.Equal("E-Mail oder API-Token ist falsch.", harness.ViewModel.ErrorMessage);
        Assert.Null(harness.Credentials.Stored);
        Assert.Equal(0, harness.Settings.SaveCalls);
        Assert.Null(harness.LoggedIn);
        Assert.False(harness.ViewModel.IsBusy);
    }

    [Fact]
    public async Task Unreachable_jira_shows_message()
    {
        var harness = new Harness().FillValidForm();
        harness.Client.MyselfFailure = new JiraUnavailableException("offline");

        await harness.ViewModel.LoginCommand.ExecuteAsync(null);

        Assert.Equal("Jira ist unter dieser Adresse nicht erreichbar.", harness.ViewModel.ErrorMessage);
    }

    [Fact]
    public async Task Server_error_shows_status_code()
    {
        var harness = new Harness().FillValidForm();
        harness.Client.MyselfFailure = new JiraUnavailableException("kaputt", 503);

        await harness.ViewModel.LoginCommand.ExecuteAsync(null);

        Assert.Equal("Anmeldung fehlgeschlagen: HTTP 503.", harness.ViewModel.ErrorMessage);
    }

    [Fact]
    public async Task Rate_limit_during_login_shows_generic_message()
    {
        var harness = new Harness().FillValidForm();
        harness.Client.MyselfFailure = new JiraRateLimitException();

        await harness.ViewModel.LoginCommand.ExecuteAsync(null);

        Assert.Equal("Anmeldung fehlgeschlagen.", harness.ViewModel.ErrorMessage);
    }

    [Fact]
    public async Task Unexpected_exception_during_login_shows_message_and_reenables_button()
    {
        var harness = new Harness().FillValidForm();
        harness.Client.MyselfFailure = new InvalidOperationException("boom");

        await harness.ViewModel.LoginCommand.ExecuteAsync(null);

        Assert.Equal(
            "Es ist ein unerwarteter Fehler aufgetreten. Bitte erneut versuchen; Einzelheiten stehen im Protokoll.",
            harness.ViewModel.ErrorMessage);
        Assert.False(harness.ViewModel.IsBusy);
        Assert.True(harness.ViewModel.LoginCommand.CanExecute(null));
        Assert.Null(harness.LoggedIn);
    }
}
