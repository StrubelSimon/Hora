using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hora.App.Services;
using Hora.Core.Jira;
using Hora.Core.Jira.Models;
using Hora.Core.Logging;
using Hora.Core.Storage;

namespace Hora.App.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    public const string TokenHelpUrl = "https://id.atlassian.com/manage-profile/security/api-tokens";

    private readonly Func<string, string, string, IJiraClient> _clientFactory;
    private readonly ISettingsStore _settingsStore;
    private readonly ICredentialStore _credentialStore;
    private readonly IAppLogger _logger;

    public LoginViewModel(
        AppSettings settings,
        Func<string, string, string, IJiraClient> clientFactory,
        ISettingsStore settingsStore,
        ICredentialStore credentialStore,
        IAppLogger logger)
    {
        _clientFactory = clientFactory;
        _settingsStore = settingsStore;
        _credentialStore = credentialStore;
        _logger = logger;
        _baseUrl = settings.BaseUrl;
        _email = settings.Email;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _baseUrl;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _email;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _apiToken = string.Empty;

    [ObservableProperty]
    private bool _rememberMe = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    [NotifyPropertyChangedFor(nameof(LoginButtonText))]
    private bool _isBusy;

    public bool HasError => ErrorMessage is not null;

    public string LoginButtonText => IsBusy ? "Zugangsdaten werden geprüft …" : "Anmelden";

    /// <summary>Erfolgreiche Anmeldung: Client, Nutzer und bereinigte Basis-URL.</summary>
    public event Action<IJiraClient, JiraUser, string>? LoggedIn;

    private bool CanLogin() =>
        !IsBusy
        && !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(Email)
        && !string.IsNullOrWhiteSpace(ApiToken);

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        ErrorMessage = null;
        var baseUrl = BaseUrl.Trim().TrimEnd('/');
        var email = Email.Trim();

        if (!baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Die Jira-URL muss mit https:// beginnen.";
            return;
        }

        IsBusy = true;
        try
        {
            var client = _clientFactory(baseUrl, email, ApiToken);
            var user = await client.GetMyselfAsync(CancellationToken.None);

            // Bestehende Einstellungen laden und nur die beiden Felder dieses Formulars ändern. Ein
            // frisches AppSettings würde Sollstunden und Designwahl auf die Vorbelegung zurücksetzen —
            // settings.json ist die einzige Stelle, an der sich die Sollstunden einstellen lassen, und
            // eine Teilzeit-Woche wäre nach jeder erneuten Anmeldung stillschweigend wieder acht Stunden.
            var settings = _settingsStore.Load();
            settings.BaseUrl = baseUrl;
            settings.Email = email;
            _settingsStore.Save(settings);
            if (RememberMe)
            {
                _credentialStore.Save(new StoredCredentials(baseUrl, email, ApiToken));
            }
            else
            {
                _credentialStore.Delete();
            }

            LoggedIn?.Invoke(client, user, baseUrl);
        }
        catch (JiraAuthenticationException)
        {
            ErrorMessage = "E-Mail oder API-Token ist falsch.";
        }
        catch (JiraUnavailableException ex) when (ex.StatusCode is null)
        {
            ErrorMessage = "Jira ist unter dieser Adresse nicht erreichbar.";
        }
        catch (JiraException ex)
        {
            _logger.Error("Anmeldung fehlgeschlagen", ex);
            ErrorMessage = ex is JiraUnavailableException { StatusCode: { } status }
                ? $"Anmeldung fehlgeschlagen: HTTP {status}."
                : "Anmeldung fehlgeschlagen.";
        }
        catch (UriFormatException)
        {
            ErrorMessage = "Die Jira-URL ist ungültig.";
        }
        catch (Exception ex)
        {
            // Auffangzweig hinter allen spezifischen Zweigen (Task 13, Fix-Runde 1): eine unerwartete
            // Ausnahme darf nicht lautlos bleiben, auch wenn IsBusy über das finally ohnehin zurückgesetzt
            // wird und die Schaltfläche wieder bedienbar ist. Ohne Meldung stünde der Nutzer sonst ohne
            // jede Erklärung vor dem unveränderten Formular.
            _logger.Error("Anmeldung unerwartet fehlgeschlagen", ex);
            ErrorMessage = "Es ist ein unerwarteter Fehler aufgetreten. Bitte erneut versuchen; Einzelheiten stehen im Protokoll.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenTokenHelp() => BrowserLauncher.Open(TokenHelpUrl);
}
