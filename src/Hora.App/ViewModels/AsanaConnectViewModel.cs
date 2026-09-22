using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hora.App.Services;
using Hora.Core.Asana;
using Hora.Core.Asana.Models;
using Hora.Core.Storage;

namespace Hora.App.ViewModels;

/// <summary>
/// Das Verbinden-Fenster in zwei Schritten: erst der Token, dann Bestätigung der Identität und
/// Wahl des Workspace. Die E-Mail wird bewusst nicht abgefragt — ein Asana-Token ist ein
/// Bearer-Token ohne Benutzerkennung, eine eingetippte E-Mail wäre eine ungeprüfte Behauptung.
/// </summary>
/// <remarks>
/// Kennt keine WPF-Typen; das Fenster hängt allein über Bindungen und das Ereignis
/// <see cref="Connected"/> daran. So bleibt die Seite ohne STA-Faden prüfbar.
/// </remarks>
public partial class AsanaConnectViewModel : ObservableObject
{
    public const string TokenHelpUrl = "https://app.asana.com/0/my-apps";

    private readonly Func<string, IAsanaClient> _clientFactory;
    private readonly IAsanaCredentialStore _store;

    private AsanaUser? _user;

    /// <summary>
    /// Der Token, mit dem die Identität in <see cref="_user"/> tatsächlich geprüft wurde.
    /// Gespeichert wird dieser, nicht der Inhalt des Eingabefelds: Beides kann auseinanderlaufen,
    /// sobald jemand zurückgeht und weitertippt, und dann gehörten Token und Kennung zu
    /// verschiedenen Konten.
    /// </summary>
    private string _verifiedToken = string.Empty;

    public AsanaConnectViewModel(Func<string, IAsanaClient> clientFactory, IAsanaCredentialStore store)
    {
        _clientFactory = clientFactory;
        _store = store;
    }

    /// <summary>Die Verbindung steht und ist gespeichert.</summary>
    public event Action? Connected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    private string _token = string.Empty;

    /// <summary>
    /// Ein geänderter Token entwertet die geprüfte Identität: Was hier steht, ist wieder eine
    /// ungeprüfte Behauptung, bis „Weiter“ sie bei Asana bestätigt hat.
    /// </summary>
    partial void OnTokenChanged(string value)
    {
        _user = null;
        ConnectCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWorkspaceStep))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private bool _isTokenStep = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    [NotifyPropertyChangedFor(nameof(ContinueButtonText))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _userEmail = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private AsanaWorkspace? _selectedWorkspace;

    public ObservableCollection<AsanaWorkspace> Workspaces { get; } = new();

    public bool IsWorkspaceStep => !IsTokenStep;

    public bool HasError => ErrorMessage is not null;

    public string ContinueButtonText => IsBusy ? "Token wird geprüft …" : "Weiter";

    private bool CanContinue() => !IsBusy && !string.IsNullOrWhiteSpace(Token);

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private async Task ContinueAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var user = await _clientFactory(Token.Trim()).GetMyselfAsync(CancellationToken.None);

            if (user.Workspaces.Count == 0)
            {
                ErrorMessage = "Zu diesem Token gehört kein Workspace. Bitte den Zugang in Asana prüfen.";
                return;
            }

            _user = user;
            _verifiedToken = Token.Trim();
            UserName = user.Name;
            UserEmail = user.Email ?? string.Empty;

            Workspaces.Clear();
            foreach (var workspace in user.Workspaces)
            {
                Workspaces.Add(workspace);
            }

            // Bei genau einem Workspace ist die Wahl keine — er steht vorausgewählt da.
            SelectedWorkspace = Workspaces[0];
            IsTokenStep = false;
        }
        catch (AsanaAuthenticationException)
        {
            ErrorMessage = "Asana hat den Token abgelehnt. Bitte prüfen und erneut versuchen.";
        }
        catch (AsanaUnavailableException)
        {
            ErrorMessage = "Asana ist nicht erreichbar. Netzwerkverbindung prüfen.";
        }
        catch (AsanaException)
        {
            ErrorMessage = "Die Verbindung zu Asana ist fehlgeschlagen.";
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

    private bool CanConnect() => IsWorkspaceStep && _user is not null && SelectedWorkspace is not null;

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private void Connect()
    {
        // Der Wächter wiederholt CanConnect: Execute fragt es von sich aus nicht ab, und genau
        // dieser Weg dürfte niemals einen ungeprüften Token speichern.
        if (!IsWorkspaceStep || _user is null || SelectedWorkspace is null)
        {
            return;
        }

        _store.Save(new AsanaCredentials(
            _verifiedToken,
            _user.Gid,
            _user.Name,
            SelectedWorkspace.Gid,
            SelectedWorkspace.Name));

        Connected?.Invoke();
    }

    /// <summary>Zurück zum Token-Schritt. Der eingegebene Token bleibt stehen — meist ist nur ein Tippfehler zu beheben.</summary>
    [RelayCommand]
    private void Back()
    {
        ErrorMessage = null;
        IsTokenStep = true;
    }

    [RelayCommand]
    private void OpenTokenHelp() => BrowserLauncher.Open(TokenHelpUrl);
}
