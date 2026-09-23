using System.Windows;
using Hora.App.Services;
using Hora.App.ViewModels;
using Hora.Core.Jira;
using Hora.Core.Logging;
using Hora.Core.Storage;

namespace Hora.App;

public partial class App : Application
{
    // Beide leben so lange wie die App und werden in OnExit wieder abgeräumt.
    private RegistrySystemThemeReader? _systemThemeReader;
    private ThemeManager? _themeManager;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = AppPaths.Default();
        var logger = new FileLogger(paths);

        DispatcherUnhandledException += (_, args) =>
        {
            logger.Error("Unbehandelter Fehler in der Oberfläche", args.Exception);
            MessageBox.Show("Unerwarteter Fehler. Details in hora.log.", "Hora", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            logger.Error("Unbeobachteter Task-Fehler", args.Exception);
            args.SetObserved();
        };

        try
        {
            ShowMainWindow(paths, logger);
        }
        catch (Exception ex)
        {
            // Der gesamte Startweg liegt in diesem Zweig, weil OnStartup über den Dispatcher läuft:
            // Eine Ausnahme von hier landete im Fehlerbehandler oben, der Handled = true setzt. Mit
            // ShutdownMode="OnMainWindowClose" und ohne je erzeugtes Fenster fände die App danach nie
            // eine Beendigungsbedingung und liefe als unsichtbarer Prozess weiter — jeder weitere
            // Doppelklick erzeugte einen zusätzlichen (Befund C). Deshalb hier eine sichtbare Meldung
            // und ein ausdrückliches Beenden.
            logger.Error("Start fehlgeschlagen", ex);
            MessageBox.Show(
                "Hora konnte nicht gestartet werden. Details in hora.log.",
                "Hora",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void ShowMainWindow(AppPaths paths, IAppLogger logger)
    {
        var settingsStore = new JsonSettingsStore(paths);

        // Die Windows-Einstellung wird über SystemEvents beobachtet. Die Meldung kommt auf einem
        // fremden Thread; der Designwechsel fasst Oberfläche an und muss deshalb zurück auf den
        // Dispatcher. BeginInvoke statt Invoke: Der meldende Thread wartet dann nicht auf uns.
        _systemThemeReader = new RegistrySystemThemeReader(action => Dispatcher.BeginInvoke(action));

        // Das Token-Wörterbuch muss vor dem Erzeugen des Fensters an Position 0 stehen, damit die
        // DynamicResource-Verweise in Theme.xaml beim ersten Aufbau der Oberfläche bereits greifen.
        _themeManager = new ThemeManager(Resources.MergedDictionaries, _systemThemeReader);
        var themeManager = _themeManager;
        themeManager.ApplyPreference(ReadThemeChoiceOrDefault(settingsStore, logger));

        var mainViewModel = new MainViewModel(
            new DpapiCredentialStore(paths),
            settingsStore,
            logger,
            JiraClient.Create,
            () => DateOnly.FromDateTime(DateTime.Now),
            themeManager,
            now: null,
            refreshScheduler: new DispatcherRefreshScheduler(logger),
            saveFilePicker: new SaveFileDialogPicker());

        var window = new MainWindow { DataContext = mainViewModel };
        window.Show();

        _ = mainViewModel.StartAsync();
    }

    /// <summary>
    /// Liest die Designwahl („System“, „Light“ oder „Dark“). Scheitert das Lesen, startet die App
    /// im hellen Modus statt gar nicht: Dieser Aufruf steht vor dem Erzeugen des Fensters, eine
    /// Ausnahme hier kostete das Fenster. JsonSettingsStore.Load fängt seit Befund C selbst alles
    /// ab; dieser Zweig ist die zweite Sicherung für den Fall, dass eine künftige Fassung wieder
    /// etwas durchlässt.
    /// </summary>
    private static ThemePreference ReadThemeChoiceOrDefault(ISettingsStore settingsStore, IAppLogger logger)
    {
        try
        {
            return ThemePreferenceNames.Parse(settingsStore.Load().Theme);
        }
        catch (Exception ex)
        {
            logger.Error("Einstellungen konnten beim Start nicht gelesen werden; heller Modus als Rückfall", ex);
            return ThemePreference.Light;
        }
    }

    /// <summary>
    /// Löst die Anmeldung an SystemEvents. Ohne das bliebe der Leser über die statische
    /// Anmeldungsliste von SystemEvents bis zum Prozessende am Leben.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        _themeManager?.Dispose();
        _systemThemeReader?.Dispose();
        base.OnExit(e);
    }
}
