using CommunityToolkit.Mvvm.ComponentModel;
using Hora.App.Services;
using Hora.Core.Everhour;
using Hora.Core.Jira;
using Hora.Core.Jira.Models;
using Hora.Core.Logging;
using Hora.Core.Storage;
using Hora.Core.Worklogs;

namespace Hora.App.ViewModels;

/// <summary>Wurzel-ViewModel: entscheidet, ob Login oder Übersicht angezeigt wird.</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ICredentialStore _credentialStore;
    private readonly ISettingsStore _settingsStore;
    private readonly IAppLogger _logger;
    private readonly Func<string, string, string, IJiraClient> _clientFactory;
    private readonly Func<DateOnly> _today;
    private readonly IThemeSwitcher? _themeManager;
    private readonly IRefreshScheduler? _refreshScheduler;
    private readonly IEverhourCredentialStore? _everhourCredentialStore;
    private readonly Func<string, IEverhourClient>? _everhourClientFactory;

    // Nur durchgereicht: Gebraucht wird der Speichern-Dialog allein vom Export der Profilseite.
    private readonly ISaveFilePicker? _saveFilePicker;

    // Eigene Uhr für den Zeitpunkt der letzten Synchronisation, damit dieser Zeitpunkt prüfbar ist.
    private readonly Func<DateTimeOffset> _now;

    // Lebt, solange die Übersicht lebt. Über ihn wechselt die Profilseite den Asana-Steckplatz,
    // ohne dass die Übersicht neu aufgebaut und das gewählte Datum verloren geht.
    private CompositeWorklogService? _worklogs;

    public MainViewModel(
        ICredentialStore credentialStore,
        ISettingsStore settingsStore,
        IAppLogger logger,
        Func<string, string, string, IJiraClient> clientFactory,
        Func<DateOnly> today,
        IThemeSwitcher? themeManager = null,
        Func<DateTimeOffset>? now = null,
        IRefreshScheduler? refreshScheduler = null,
        ISaveFilePicker? saveFilePicker = null,
        IEverhourCredentialStore? everhourCredentialStore = null,
        Func<string, IEverhourClient>? everhourClientFactory = null)
    {
        _credentialStore = credentialStore;
        _settingsStore = settingsStore;
        _logger = logger;
        _clientFactory = clientFactory;
        _today = today;
        // Wird nur noch durchgereicht: Das Design wird auf der Profilseite gewählt (siehe
        // ShowOverview), seit die Umschaltpille aus der Fensterleiste entfallen ist.
        _themeManager = themeManager;
        _now = now ?? (() => DateTimeOffset.Now);
        // Ein Taktgeber für die ganze Laufzeit: Er hängt jeweils an der lebenden Übersicht und
        // steht still, solange keine da ist (Login, nach dem Abmelden).
        _refreshScheduler = refreshScheduler;
        _saveFilePicker = saveFilePicker;
        _everhourCredentialStore = everhourCredentialStore;
        _everhourClientFactory = everhourClientFactory;
    }

    public string AppTitle => $"Hora {AppVersion.Current}";

    [ObservableProperty]
    private ObservableObject? _currentScreen;

    [ObservableProperty]
    private bool _isStarting = true;

    /// <summary>Beim Programmstart: gespeicherte Anmeldung prüfen oder Login zeigen.</summary>
    public async Task StartAsync()
    {
        try
        {
            var stored = _credentialStore.Load();
            if (stored is null)
            {
                ShowLogin(null);
                return;
            }

            try
            {
                var client = _clientFactory(stored.BaseUrl, stored.Email, stored.ApiToken);
                var user = await client.GetMyselfAsync(CancellationToken.None);
                ShowOverview(client, user, stored.BaseUrl);
            }
            catch (JiraAuthenticationException)
            {
                _credentialStore.Delete();
                _everhourCredentialStore?.Delete();
                ShowLogin("Gespeicherte Anmeldung ist ungültig. Bitte erneut anmelden.");
            }
            catch (JiraException ex)
            {
                _logger.Error("Automatische Anmeldung fehlgeschlagen", ex);
                ShowLogin("Jira ist nicht erreichbar.");
            }
        }
        catch (Exception ex)
        {
            // Der Auffangzweig steht bewusst hinter den beiden Jira-spezifischen Zweigen, damit die
            // Unterscheidung zwischen abgelehnter Anmeldung und Netzwerkfehler erhalten bleibt. Er
            // liegt zugleich außen um den inneren try/catch, damit auch eine unerwartete Ausnahme aus
            // _credentialStore.Load() (z. B. UnauthorizedAccessException bei restriktiven Zugriffsrechten
            // auf credentials.dat) abgefangen wird. Ohne diesen Zweig würde die von App.OnStartup mit "_ ="
            // verworfene Aufgabe unbeobachtet fehlschlagen und der Nutzer vor einem leeren, eingefrorenen
            // Fenster sitzen (Task 13, Fix-Runde 1).
            _logger.Error("Automatischer Anmeldeversuch beim Start unerwartet fehlgeschlagen", ex);
            ShowLogin("Es ist ein unerwarteter Fehler aufgetreten. Bitte erneut anmelden; Einzelheiten stehen im Protokoll.");
        }
        finally
        {
            IsStarting = false;
        }
    }

    private void ShowLogin(string? message)
    {
        var login = new LoginViewModel(_settingsStore.Load(), _clientFactory, _settingsStore, _credentialStore, _logger)
        {
            ErrorMessage = message,
        };
        login.LoggedIn += OnLoggedInViaForm;
        CurrentScreen = login;
    }

    /// <summary>
    /// Anmeldung über das Formular ist eine frische Identitätsbehauptung — anders als die
    /// automatische Anmeldung in StartAsync mit den gespeicherten Zugangsdaten, die dieselbe
    /// Identität fortsetzt und deshalb everhour.dat unangetastet lässt. Hier meldet sich möglicherweise
    /// jemand anderes an; eine stehengebliebene Asana-Verbindung des Vorgängers würde sonst den
    /// Steckplatz der neuen Identität besetzen. Deshalb fällt sie hier, bevor die Übersicht entsteht.
    /// </summary>
    private void OnLoggedInViaForm(IJiraClient client, JiraUser user, string baseUrl)
    {
        _everhourCredentialStore?.Delete();
        ShowOverview(client, user, baseUrl);
    }

    private void ShowOverview(IJiraClient client, JiraUser user, string baseUrl)
    {
        var zone = TimeZoneInfo.Local;
        LogTimeZoneMismatch(user.TimeZone, zone);

        var jiraWorklogs = new JiraWorklogService(client, user.AccountId, baseUrl, zone, _logger);
        var worklogs = new CompositeWorklogService(jiraWorklogs, _logger, zone);
        _worklogs = worklogs;
        worklogs.AsanaTokenRejected += OnAsanaTokenRejected;
        ApplyAsanaConnection();

        var settings = _settingsStore.Load();
        // Diese eine Instanz lebt, solange die Übersicht lebt; der Schrittschalter der Profilseite
        // ändert genau sie, damit Kennzahlen und Balken sofort mit den neuen Sollstunden rechnen.
        var schedule = settings.Schedule;
        var overview = new OverviewViewModel(
            worklogs,
            user,
            zone,
            _logger,
            _today,
            schedule,
            ViewModeNames.Parse(settings.StartView),
            DurationFormatter.Parse(settings.TimeFormat),
            _refreshScheduler);

        // Der Takt beginnt mit dem gespeicherten Abstand zu laufen; der erste Abruf unten ist der
        // Startabruf und nicht der Takt.
        overview.AutoRefreshMinutes = RefreshIntervals.Parse(settings.RefreshIntervalMinutes);

        overview.LogoutRequested += () =>
        {
            StopAutoRefresh(overview);
            _credentialStore.Delete();
            _everhourCredentialStore?.Delete();
            ShowLogin(null);
        };
        overview.SessionExpired += () =>
        {
            StopAutoRefresh(overview);
            _credentialStore.Delete();
            _everhourCredentialStore?.Delete();
            ShowLogin("Die Anmeldung ist abgelaufen oder wurde widerrufen.");
        };
        overview.SyncSucceeded += StoreLastSync;
        // Der Export der Profilseite holt seinen Monat über denselben Abrufdienst wie die Übersicht.
        overview.ProfileRequested += () => ShowProfile(overview, user, baseUrl, schedule, worklogs, zone);

        CurrentScreen = overview;
        _ = RunInitialLoadAsync(overview);
    }

    /// <summary>
    /// Besetzt den Asana-Steckplatz aus der gespeicherten Verbindung oder leert ihn, wenn keine
    /// da ist. Wird beim Aufbau der Übersicht und nach jedem Verbinden oder Trennen aufgerufen.
    /// </summary>
    private void ApplyAsanaConnection()
    {
        if (_worklogs is null)
        {
            return;
        }

        try
        {
            var credentials = _everhourCredentialStore?.Load();
            if (credentials is null || _everhourClientFactory is null)
            {
                _worklogs.SetAsanaProvider(null);
                return;
            }

            var client = _everhourClientFactory(credentials.ApiKey);
            _worklogs.SetAsanaProvider(new EverhourWorklogService(client, credentials.UserId, TimeZoneInfo.Local));
        }
        catch (Exception ex)
        {
            // Eine unlesbare everhour.dat oder ein Fehler beim Bauen des Clients darf die Übersicht
            // nicht kosten: Ohne Asana zeigt Hora weiterhin die Jira-Zeiten.
            _logger.Error("Asana-Verbindung konnte nicht hergestellt werden", ex);
            _worklogs.SetAsanaProvider(null);
        }
    }

    /// <summary>
    /// Everhour hat den API-Schlüssel abgelehnt. Die gespeicherte Verbindung ist damit wertlos; sie wird
    /// gelöscht und der Steckplatz geleert, damit nicht jeder Takt erneut in dasselbe 401 läuft.
    /// Die Jira-Sitzung bleibt unberührt.
    /// </summary>
    private void OnAsanaTokenRejected()
    {
        try
        {
            _everhourCredentialStore?.Delete();
        }
        catch (Exception ex)
        {
            _logger.Error("Asana-Verbindung konnte nicht gelöscht werden", ex);
        }

        _worklogs?.SetAsanaProvider(null);
    }

    /// <summary>
    /// Die Profilseite will die Asana-Verbindung herstellen. Das Fenster öffnet die Ansicht;
    /// dieses ViewModel kennt keine WPF-Typen. Nach erfolgreichem Verbinden ruft die Ansicht
    /// <see cref="ProfileViewModel.RefreshAsanaState"/> auf.
    /// </summary>
    public event Action<ProfileViewModel>? AsanaConnectRequested;

    /// <summary>Baut das ViewModel für das Verbinden-Fenster. Die Ansicht hängt es an ein Fenster.</summary>
    public EverhourConnectViewModel? CreateEverhourConnectViewModel() =>
        _everhourClientFactory is null || _everhourCredentialStore is null
            ? null
            : new EverhourConnectViewModel(_everhourClientFactory, _everhourCredentialStore);

    /// <summary>
    /// Die Profilseite ersetzt die Übersicht als Vollbildseite. Die Übersicht bleibt dabei am Leben:
    /// „Fertig“ und der Zurück-Pfeil zeigen wieder genau dieselbe Instanz, ohne neuen Abruf.
    /// </summary>
    private void ShowProfile(
        OverviewViewModel overview,
        JiraUser user,
        string baseUrl,
        WorkSchedule schedule,
        IWorklogService worklogs,
        TimeZoneInfo zone)
    {
        var profile = new ProfileViewModel(
            _settingsStore,
            user,
            baseUrl,
            schedule,
            _themeManager,
            worklogs,
            zone,
            _logger,
            _saveFilePicker,
            _now,
            _everhourCredentialStore);

        // Der Steckplatz allein genügt hier nicht: Die Übersicht steht mit den Zeilen da, die sie
        // vor dem Wechsel geholt hat. Ohne erzwungenen Neuabruf erschienen die Asana-Zeiten nach dem
        // Verbinden erst beim nächsten Takt — und nach dem Trennen blieben sie ebenso lange stehen.
        //
        // Neu geladen wird ausdrücklich nur auf diesem Weg und nicht in ApplyAsanaConnection selbst:
        // Jenes ruft auch ShowOverview beim Aufbau, und dort stößt RunInitialLoadAsync den ersten
        // Abruf ohnehin an. Läge das Neuladen dort drin, liefe beim Start jedes Mal ein zweiter,
        // überflüssiger Abruf.
        profile.AsanaConnectionChanged += () =>
        {
            ApplyAsanaConnection();
            _ = RunAsanaReloadAsync(overview);
        };
        profile.AsanaConnectRequested += () => AsanaConnectRequested?.Invoke(profile);

        profile.CloseRequested += () =>
        {
            profile.Detach();
            CurrentScreen = overview;
        };
        profile.LogoutRequested += () =>
        {
            profile.Detach();
            StopAutoRefresh(overview);
            _credentialStore.Delete();
            _everhourCredentialStore?.Delete();
            ShowLogin(null);
        };
        profile.DisplaySettingsChanged += () =>
        {
            // Das Setzen des Zeitformats zeichnet die Übersicht bereits neu; der zweite Aufruf deckt
            // die Sollstunden mit ab, die dieselbe WorkSchedule-Instanz verändert haben.
            overview.DurationFormat = profile.DurationFormat;
            overview.AutoRefreshMinutes = profile.RefreshIntervalMinutes;
            overview.RefreshDisplay();
        };

        CurrentScreen = profile;
    }

    /// <summary>
    /// Hält den Takt an, wenn die Übersicht aus dem Bild geht. Ohne das liefe er über das Abmelden
    /// hinaus weiter und holte Buchungen für eine Sitzung, die es nicht mehr gibt.
    /// </summary>
    private static void StopAutoRefresh(OverviewViewModel overview) => overview.AutoRefreshMinutes = 0;

    /// <summary>Hält den Zeitpunkt des letzten erfolgreichen Abrufs fest, damit er einen Neustart übersteht.</summary>
    private void StoreLastSync()
    {
        try
        {
            var settings = _settingsStore.Load();
            settings.LastSyncAt = _now();
            // Nur mit bestehender Verbindung: Ohne sie stünde in der Asana-Zeile der Profilseite
            // ein Zeitpunkt, zu dem nie etwas von Asana geholt wurde.
            if (_everhourCredentialStore?.Load() is not null)
            {
                settings.AsanaLastSyncAt = settings.LastSyncAt;
            }

            _settingsStore.Save(settings);
        }
        catch (Exception ex)
        {
            // Ein nicht schreibbares settings.json darf den gerade geglückten Abruf nicht entwerten:
            // Die Buchungen stehen bereits auf dem Schirm, nur der Zeitstempel fehlt dann.
            _logger.Error("Zeitpunkt der letzten Synchronisation konnte nicht gespeichert werden", ex);
        }
    }

    private async Task RunInitialLoadAsync(OverviewViewModel overview)
    {
        try
        {
            await overview.LoadAsync();
        }
        catch (Exception ex)
        {
            // LoadAsync fängt Jira-Fehler selbst ab; hier landen nur unerwartete Fehler.
            _logger.Error("Erstes Laden der Buchungen fehlgeschlagen", ex);
        }
    }

    /// <summary>
    /// Neuabruf, nachdem die Asana-Verbindung hergestellt oder getrennt wurde. Erzwungen, weil der
    /// zwischengespeicherte Stand noch zur alten Verbindung gehört und ein gewöhnlicher Abruf ihn
    /// als bereits geladen ansähe.
    /// </summary>
    /// <remarks>
    /// Nicht abgewartet — das Ereignis ist ein <see cref="Action"/>, und die Profilseite soll nicht
    /// stehen bleiben, während im Hintergrund geholt wird. Die Fehler fängt dieser Zweig trotzdem
    /// ab: Ein nicht beobachteter Task verlöre sie sonst lautlos.
    /// </remarks>
    private async Task RunAsanaReloadAsync(OverviewViewModel overview)
    {
        try
        {
            await overview.LoadAsync(forceReload: true);
        }
        catch (Exception ex)
        {
            // LoadAsync fängt Jira-Fehler selbst ab; hier landen nur unerwartete Fehler.
            _logger.Error("Neuladen nach Wechsel der Asana-Verbindung fehlgeschlagen", ex);
        }
    }

    private void LogTimeZoneMismatch(string? jiraTimeZone, TimeZoneInfo localZone)
    {
        if (string.IsNullOrEmpty(jiraTimeZone))
        {
            return;
        }

        var jiraAsWindowsId = TimeZoneInfo.TryConvertIanaIdToWindowsId(jiraTimeZone, out var windowsId) ? windowsId : jiraTimeZone;
        if (!string.Equals(jiraAsWindowsId, localZone.Id, StringComparison.OrdinalIgnoreCase))
        {
            _logger.Info($"Jira-Zeitzone '{jiraTimeZone}' weicht von der lokalen Zone '{localZone.Id}' ab. Tagesgrenzen folgen der lokalen Zone.");
        }
    }
}
