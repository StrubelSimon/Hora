using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hora.App.Services;
using Hora.Core.Everhour;
using Hora.Core.Storage;

namespace Hora.App.ViewModels;

/// <summary>
/// Das Verbinden-Fenster für die Asana-Zeiten: ein Schritt, der den Everhour-Schlüssel prüft und
/// speichert. Anders als bei Asana gibt es keinen Workspace zu wählen — ein Everhour-Schlüssel
/// gehört genau einem Nutzer in genau einem Team.
/// </summary>
/// <remarks>
/// Kennt keine WPF-Typen; das Fenster hängt allein über Bindungen und das Ereignis
/// <see cref="Connected"/> daran. So bleibt die Seite ohne STA-Faden prüfbar.
/// </remarks>
public partial class EverhourConnectViewModel : ObservableObject
{
    /// <summary>In Everhour unter „Profil“ → „Application Access“ steht der persönliche API-Schlüssel.</summary>
    public const string ApiKeyHelpUrl = "https://app.everhour.com/#/account/profile";

    private readonly Func<string, IEverhourClient> _clientFactory;
    private readonly IEverhourCredentialStore _store;

    public EverhourConnectViewModel(Func<string, IEverhourClient> clientFactory, IEverhourCredentialStore store)
    {
        _clientFactory = clientFactory;
        _store = store;
    }

    /// <summary>Die Verbindung steht und ist gespeichert.</summary>
    public event Action? Connected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyPropertyChangedFor(nameof(ConnectButtonText))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => ErrorMessage is not null;

    public string ConnectButtonText => IsBusy ? "Schlüssel wird geprüft …" : "Verbinden";

    private bool CanConnect() => !IsBusy && !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>
    /// Gespeichert wird nur ein Schlüssel, den Everhour gerade bestätigt hat, zusammen mit der
    /// Nutzerkennung aus genau dieser Antwort — nie eine eingetippte Behauptung.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var apiKey = ApiKey.Trim();
            var user = await _clientFactory(apiKey).GetMyselfAsync(CancellationToken.None);

            if (user.Id <= 0)
            {
                ErrorMessage = "Everhour hat zu diesem Schlüssel keinen Nutzer geliefert. Bitte den Schlüssel prüfen.";
                return;
            }

            _store.Save(new EverhourCredentials(apiKey, user.Id, user.Name));
            Connected?.Invoke();
        }
        catch (EverhourAuthenticationException)
        {
            ErrorMessage = "Everhour hat den Schlüssel abgelehnt. Bitte prüfen und erneut versuchen.";
        }
        catch (EverhourUnavailableException)
        {
            ErrorMessage = "Everhour ist nicht erreichbar. Netzwerkverbindung prüfen.";
        }
        catch (EverhourException)
        {
            ErrorMessage = "Die Verbindung zu Everhour ist fehlgeschlagen.";
        }
        catch (Exception)
        {
            // Auffangzweig hinter den spezifischen: Ohne Meldung stünde der Nutzer vor einem
            // unveränderten Formular und wüsste nicht, ob überhaupt etwas passiert ist.
            ErrorMessage = "Es ist ein unerwarteter Fehler aufgetreten. Bitte erneut versuchen.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenApiKeyHelp() => BrowserLauncher.Open(ApiKeyHelpUrl);
}
