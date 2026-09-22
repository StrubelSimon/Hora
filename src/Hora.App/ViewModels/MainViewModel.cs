using CommunityToolkit.Mvvm.ComponentModel;
using Hora.App.Services;
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

    // Eigene Uhr für den Zeitpunkt der letzten Synchronisation, damit dieser Zeitpunkt prüfbar ist.
    private readonly Func<DateTimeOffset> _now;

    public MainViewModel(
        ICredentialStore credentialStore,
        ISettingsStore settingsStore,
        IAppLogger logger,
        Func<string, string, string, IJiraClient> clientFactory,
        Func<DateOnly> today,
        IThemeSwitcher? themeManager = null,
        Func<DateTimeOffset>? now = null,
        IRefreshScheduler? refreshScheduler = null)
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
        login.LoggedIn += ShowOverview;
        CurrentScreen = login;
    }

    private void ShowOverview(IJiraClient client, JiraUser user, string baseUrl)
    {
        var zone = TimeZoneInfo.Local;
        LogTimeZoneMismatch(user.TimeZone, zone);

        var worklogs = new WorklogService(client, user.AccountId, zone, _logger);
        var settings = _settingsStore.Load();
        // Diese eine Instanz lebt, solange die Übersicht lebt; der Schrittschalter der Profilseite
        // ändert genau sie, damit Kennzahlen und Balken sofort mit den neuen Sollstunden rechnen.
        var schedule = settings.Schedule;
        var overview = new OverviewViewModel(
            worklogs,
            user,
            baseUrl,
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
            ShowLogin(null);
        };
        overview.SessionExpired += () =>
        {
            StopAutoRefresh(overview);
            _credentialStore.Delete();
            ShowLogin("Die Anmeldung ist abgelaufen oder wurde widerrufen.");
        };
        overview.SyncSucceeded += StoreLastSync;
        overview.ProfileRequested += () => ShowProfile(overview, user, baseUrl, schedule);

        CurrentScreen = overview;
        _ = RunInitialLoadAsync(overview);
    }

    /// <summary>
    /// Die Profilseite ersetzt die Übersicht als Vollbildseite. Die Übersicht bleibt dabei am Leben:
    /// „Fertig“ und der Zurück-Pfeil zeigen wieder genau dieselbe Instanz, ohne neuen Abruf.
    /// </summary>
    private void ShowProfile(OverviewViewModel overview, JiraUser user, string baseUrl, WorkSchedule schedule)
    {
        var profile = new ProfileViewModel(_settingsStore, user, baseUrl, schedule, _themeManager);

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
