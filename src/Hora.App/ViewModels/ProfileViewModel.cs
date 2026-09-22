using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hora.App.Services;
using Hora.Core.Jira.Models;
using Hora.Core.Storage;
using Hora.Core.Worklogs;

namespace Hora.App.ViewModels;

/// <summary>
/// Die Profilseite: eine Vollbildseite, die die Übersicht ersetzt. Jede Änderung wirkt sofort und
/// wird sofort gespeichert; es gibt keinen Speichern-Knopf. „Fertig“ und der Zurück-Pfeil
/// navigieren nur.
/// </summary>
/// <remarks>
/// Die Seite kennt keine WPF-Typen: Der Designwechsel läuft über <see cref="IThemeSwitcher"/>, die
/// Rückfrage vor dem Abmelden liegt in der Ansicht. So bleibt sie ohne STA-Thread prüfbar.
/// </remarks>
public partial class ProfileViewModel : ObservableObject
{
    /// <summary>Schrittweite des Schrittschalters „Sollstunden pro Tag“: eine halbe Stunde.</summary>
    private const double TargetHoursStep = 0.5;

    private const string LastSyncFormat = "dd.MM.yyyy, HH:mm";

    private const string NoLastSyncText = "–";

    private readonly ISettingsStore _settingsStore;
    private readonly IThemeSwitcher? _themeSwitcher;
    private readonly IAsanaCredentialStore? _asanaCredentialStore;

    // Dieselbe Instanz, an der auch die Übersicht hängt: Ändert der Schrittschalter die
    // Sollstunden, rechnen Kennzahlen und Balken sofort mit den neuen Werten.
    private readonly WorkSchedule _schedule;

    private readonly Action<AppTheme>? _themeChangedHandler;

    public ProfileViewModel(
        ISettingsStore settingsStore,
        JiraUser user,
        string baseUrl,
        WorkSchedule schedule,
        IThemeSwitcher? themeSwitcher = null,
        IAsanaCredentialStore? asanaCredentialStore = null)
    {
        _settingsStore = settingsStore;
        _themeSwitcher = themeSwitcher;
        _schedule = schedule;

        var settings = settingsStore.Load();

        DisplayName = user.DisplayName;
        Email = user.EmailAddress ?? settings.Email;
        Initials = InitialsFormatter.From(user.DisplayName);
        Role = settings.Role;

        InstanceUrl = baseUrl.TrimEnd('/');
        InstanceText = StripScheme(InstanceUrl);
        LastSyncText = settings.LastSyncAt is { } lastSync
            ? lastSync.ToString(LastSyncFormat, CultureInfo.InvariantCulture)
            : NoLastSyncText;

        // Felder statt Eigenschaften: Das Übernehmen des gespeicherten Stands ist keine Änderung
        // des Nutzers und darf deshalb weder speichern noch ein Neuzeichnen auslösen.
        _startView = ViewModeNames.Parse(settings.StartView);
        _durationFormat = DurationFormatter.Parse(settings.TimeFormat);
        _reminderEnabled = settings.ReminderEnabled;
        _refreshIntervalMinutes = RefreshIntervals.Parse(settings.RefreshIntervalMinutes);

        // Welche Tage Arbeitstage sind, steht — wie jede andere Anfangsbelegung dieser Seite — in
        // den gespeicherten Einstellungen: Als Arbeitstag gilt jeder Tag mit einem Wert größer null.
        // Die lebende Instanz daneben trägt dieselbe Woche; sie wird nur deshalb getrennt
        // hereingereicht, weil die Übersicht an genau diesem Objekt weiterrechnet.
        var storedSchedule = settings.Schedule ?? schedule;
        Workdays = WorkSchedule.WeekOrder
            .Select((day, index) => new WorkdayToggleViewModel(
                day,
                DayLabels[index],
                storedSchedule.IsWorkday(day),
                OnWorkdayToggled))
            .ToList();

        // Der Anzeigewert des Schrittschalters kommt aus der lebenden Woche: der erste Arbeitstag,
        // der nicht null ist.
        _targetHours = _schedule.GetWorkdayHours();

        _asanaCredentialStore = asanaCredentialStore;
        _isAsanaConnected = asanaCredentialStore?.Load() is not null;
        AsanaLastSyncText = settings.AsanaLastSyncAt is { } asanaSync
            ? asanaSync.ToString(LastSyncFormat, CultureInfo.InvariantCulture)
            : NoLastSyncText;

        if (_themeSwitcher is not null)
        {
            // Die Pille in der Fensterleiste bleibt sichtbar, während diese Seite offen ist. Wird
            // dort umgeschaltet, muss der Segmentschalter hier folgen — und umgekehrt.
            _themeChangedHandler = _ => OnThemeChanged();
            _themeSwitcher.Changed += _themeChangedHandler;
        }
    }

    /// <summary>
    /// Löst die Seite vom Designwechsler. Die Hülle ruft das beim Verlassen der Seite auf; ohne das
    /// bliebe jede je geöffnete Profilseite am langlebigen ThemeManager hängen.
    /// </summary>
    public void Detach()
    {
        if (_themeSwitcher is not null && _themeChangedHandler is not null)
        {
            _themeSwitcher.Changed -= _themeChangedHandler;
        }
    }

    // ===================== Nutzerkarte =====================

    public string DisplayName { get; }

    public string Email { get; }

    public string Initials { get; }

    public string Role { get; }

    /// <summary>Das Abzeichen bleibt ausgeblendet, solange keine Rolle eingetragen ist.</summary>
    public bool HasRole => !string.IsNullOrWhiteSpace(Role);

    // ===================== Jira =====================

    public string InstanceUrl { get; }

    /// <summary>Die Adresse ohne „https://“, wie im Entwurf.</summary>
    public string InstanceText { get; }

    public string LastSyncText { get; }

    [RelayCommand]
    private void OpenInstance() => BrowserLauncher.Open(InstanceUrl);

    // ===================== Asana =====================

    /// <summary>Nutzer will Asana verbinden; die Hülle öffnet das Fenster.</summary>
    public event Action? AsanaConnectRequested;

    /// <summary>Verbindung wurde hergestellt oder getrennt; die Hülle wechselt den Steckplatz.</summary>
    public event Action? AsanaConnectionChanged;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AsanaStatusText))]
    [NotifyPropertyChangedFor(nameof(AsanaActionText))]
    private bool _isAsanaConnected;

    public string AsanaStatusText => _asanaCredentialStore?.Load() is { } credentials
        ? $"{credentials.UserName} · {credentials.WorkspaceName}"
        : "Nicht verbunden";

    public string AsanaActionText => IsAsanaConnected ? "Trennen" : "Verbinden";

    public string AsanaLastSyncText { get; private set; } = NoLastSyncText;

    [RelayCommand]
    private void ConnectAsana() => AsanaConnectRequested?.Invoke();

    /// <summary>
    /// Wird von der Hülle gerufen, nachdem das Fenster erfolgreich verbunden hat. Liest den neuen
    /// Stand aus dem Speicher und meldet ihn weiter.
    /// </summary>
    public void RefreshAsanaState()
    {
        IsAsanaConnected = _asanaCredentialStore?.Load() is not null;
        OnPropertyChanged(nameof(AsanaStatusText));
        AsanaConnectionChanged?.Invoke();
    }

    /// <summary>Trennt die Verbindung. Die Rückfrage liegt — wie beim Abmelden — in der Ansicht.</summary>
    [RelayCommand]
    private void DisconnectAsana()
    {
        _asanaCredentialStore?.Delete();
        RefreshAsanaState();
    }

    // ===================== Automatisch aktualisieren =====================

    private int _refreshIntervalMinutes;

    /// <summary>Abstand der automatischen Aktualisierung in Minuten; null bedeutet aus.</summary>
    public int RefreshIntervalMinutes => _refreshIntervalMinutes;

    public string RefreshIntervalText => RefreshIntervals.TextFor(_refreshIntervalMinutes);

    public bool IsRefreshOff => _refreshIntervalMinutes == RefreshIntervals.Off;

    public bool IsFiveMinuteRefresh => _refreshIntervalMinutes == 5;

    public bool IsFifteenMinuteRefresh => _refreshIntervalMinutes == 15;

    public bool IsThirtyMinuteRefresh => _refreshIntervalMinutes == 30;

    [RelayCommand]
    private void TurnRefreshOff() => ApplyRefreshInterval(RefreshIntervals.Off);

    [RelayCommand]
    private void UseFiveMinuteRefresh() => ApplyRefreshInterval(5);

    [RelayCommand]
    private void UseFifteenMinuteRefresh() => ApplyRefreshInterval(15);

    [RelayCommand]
    private void UseThirtyMinuteRefresh() => ApplyRefreshInterval(30);

    private void ApplyRefreshInterval(int minutes)
    {
        if (_refreshIntervalMinutes == minutes)
        {
            return;
        }

        _refreshIntervalMinutes = minutes;
        OnPropertyChanged(nameof(RefreshIntervalMinutes));
        OnPropertyChanged(nameof(RefreshIntervalText));
        OnPropertyChanged(nameof(IsRefreshOff));
        OnPropertyChanged(nameof(IsFiveMinuteRefresh));
        OnPropertyChanged(nameof(IsFifteenMinuteRefresh));
        OnPropertyChanged(nameof(IsThirtyMinuteRefresh));

        var settings = _settingsStore.Load();
        settings.RefreshIntervalMinutes = minutes;
        _settingsStore.Save(settings);

        // Derselbe Weg zur Übersicht wie bei Zeitformat und Sollstunden; die Hülle setzt den
        // neuen Abstand dort und lässt den Takt von vorn beginnen.
        DisplaySettingsChanged?.Invoke();
    }

    // ===================== Über Hora =====================

    /// <summary>
    /// Version der laufenden Fassung, damit im Gespräch über einen Fehler feststeht, welcher Stand
    /// gemeint ist. Gepflegt wird sie an einer einzigen Stelle: &lt;Version&gt; in Hora.App.csproj.
    /// Jedes Release zählt hoch – letzte Zahl bei Fehlerbehebungen, mittlere bei neuen Funktionen,
    /// die erste nur auf ausdrückliche Ansage.
    /// </summary>
    public string VersionText { get; } = AppVersion.Current;

    // ===================== Theme =====================

    /// <summary>
    /// Der Segmentschalter zeigt die gespeicherte Wahl, nicht das wirksame Ergebnis: Steht die
    /// Wahl auf „System“, bleibt „System“ hervorgehoben, gleich ob Windows hell oder dunkel steht.
    /// </summary>
    private ThemePreference ThemeChoice => _themeSwitcher?.Preference ?? ThemePreference.Light;

    public bool IsSystemTheme => ThemeChoice == ThemePreference.System;

    public bool IsLightTheme => ThemeChoice == ThemePreference.Light;

    public bool IsDarkTheme => ThemeChoice == ThemePreference.Dark;

    [RelayCommand]
    private void UseSystemTheme() => ApplyThemeChoice(ThemePreference.System);

    [RelayCommand]
    private void UseLightTheme() => ApplyThemeChoice(ThemePreference.Light);

    [RelayCommand]
    private void UseDarkTheme() => ApplyThemeChoice(ThemePreference.Dark);

    private void ApplyThemeChoice(ThemePreference choice)
    {
        if (_themeSwitcher is null || _themeSwitcher.Preference == choice)
        {
            return;
        }

        _themeSwitcher.ApplyPreference(choice);

        // Gespeichert wird die Wahl. Das wirksame Ergebnis steht nirgends in der Datei — es hängt
        // bei „System“ an Windows und kann sich ohne Zutun der App ändern.
        var settings = _settingsStore.Load();
        settings.Theme = ThemePreferenceNames.NameOf(choice);
        _settingsStore.Save(settings);

        OnThemeChanged();
    }

    /// <summary>
    /// Wird auch gerufen, wenn sich nur das wirksame Aussehen geändert hat (Pille in der
    /// Fensterleiste oder eine Änderung der Windows-Einstellung bei der Wahl „System“).
    /// </summary>
    private void OnThemeChanged()
    {
        OnPropertyChanged(nameof(IsSystemTheme));
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
    }

    // ===================== Startansicht =====================

    private ViewMode _startView;

    public bool IsDayStartView => _startView == ViewMode.Day;

    public bool IsWeekStartView => _startView == ViewMode.Week;

    public bool IsMonthStartView => _startView == ViewMode.Month;

    [RelayCommand]
    private void UseDayStartView() => ApplyStartView(ViewMode.Day);

    [RelayCommand]
    private void UseWeekStartView() => ApplyStartView(ViewMode.Week);

    [RelayCommand]
    private void UseMonthStartView() => ApplyStartView(ViewMode.Month);

    private void ApplyStartView(ViewMode mode)
    {
        if (_startView == mode)
        {
            return;
        }

        _startView = mode;
        OnPropertyChanged(nameof(IsDayStartView));
        OnPropertyChanged(nameof(IsWeekStartView));
        OnPropertyChanged(nameof(IsMonthStartView));

        var settings = _settingsStore.Load();
        settings.StartView = ViewModeNames.NameOf(mode);
        _settingsStore.Save(settings);
    }

    // ===================== Zeitformat =====================

    private DurationFormat _durationFormat;

    public DurationFormat DurationFormat => _durationFormat;

    public bool IsHoursMinutesFormat => _durationFormat == DurationFormat.HoursMinutes;

    public bool IsDecimalHoursFormat => _durationFormat == DurationFormat.DecimalHours;

    [RelayCommand]
    private void UseHoursMinutesFormat() => ApplyDurationFormat(DurationFormat.HoursMinutes);

    [RelayCommand]
    private void UseDecimalHoursFormat() => ApplyDurationFormat(DurationFormat.DecimalHours);

    private void ApplyDurationFormat(DurationFormat format)
    {
        if (_durationFormat == format)
        {
            return;
        }

        _durationFormat = format;
        OnPropertyChanged(nameof(DurationFormat));
        OnPropertyChanged(nameof(IsHoursMinutesFormat));
        OnPropertyChanged(nameof(IsDecimalHoursFormat));
        OnPropertyChanged(nameof(TargetHoursText));

        var settings = _settingsStore.Load();
        settings.TimeFormat = format.ToString();
        _settingsStore.Save(settings);

        DisplaySettingsChanged?.Invoke();
    }

    // ===================== Sollstunden pro Tag =====================

    /// <summary>
    /// Anzeigewert des Schrittschalters. Er beginnt beim ersten Arbeitstag, der nicht null ist —
    /// eine ungleiche Woche aus settings.json bleibt deshalb erhalten, solange der Schrittschalter
    /// nicht benutzt wird. Danach ist er ein eigener Wert: Er bleibt auch dann stehen, wenn
    /// gerade kein Tag ausgewählt ist, und ist der Wert, den ein neu ausgewählter Tag bekommt.
    /// </summary>
    private double _targetHours;

    public double TargetHours => _targetHours;

    public string TargetHoursText => DurationFormatter.Format(TimeSpan.FromHours(TargetHours), _durationFormat);

    [RelayCommand]
    private void DecreaseTargetHours() => ApplyTargetHours(TargetHours - TargetHoursStep);

    [RelayCommand]
    private void IncreaseTargetHours() => ApplyTargetHours(TargetHours + TargetHoursStep);

    private void ApplyTargetHours(double hours)
    {
        var clamped = Math.Clamp(hours, WorkSchedule.MinWeekdayHours, WorkSchedule.MaxWeekdayHours);
        if (Math.Abs(clamped - TargetHours) < 0.001)
        {
            return; // Am Rand des Bereichs angekommen: nichts zu ändern, nichts zu speichern.
        }

        _targetHours = clamped;

        // Der Schrittschalter gilt für alle ausgewählten Tage; abgewählte bleiben auf null.
        foreach (var workday in Workdays.Where(day => day.IsSelected))
        {
            _schedule.SetDayHours(workday.Day, clamped);
        }

        OnPropertyChanged(nameof(TargetHours));
        OnPropertyChanged(nameof(TargetHoursText));

        SaveSchedule();
        DisplaySettingsChanged?.Invoke();
    }

    // ===================== Arbeitstage =====================

    /// <summary>Die Beschriftungen der sieben Umschalter, in der Reihenfolge von WorkSchedule.WeekOrder.</summary>
    private static readonly string[] DayLabels = { "Mo", "Di", "Mi", "Do", "Fr", "Sa", "So" };

    public IReadOnlyList<WorkdayToggleViewModel> Workdays { get; }

    /// <summary>Unterzeile der Zeile „Arbeitstage“; bei genau einem Tag in der Einzahl.</summary>
    public string WorkdayCountText
    {
        get
        {
            var count = Workdays.Count(workday => workday.IsSelected);
            return count == 1 ? "1 Tag pro Woche" : $"{count} Tage pro Woche";
        }
    }

    /// <summary>
    /// Ein Tag wurde aus- oder abgewählt. Ausgewählt bekommt er den aktuellen Wert des
    /// Schrittschalters, abgewählt bekommt er null; sind alle Tage abgewählt, ist das Wochensoll
    /// null, und die Kennzahlenkarte zeigt dafür bereits einen Gedankenstrich statt eines Prozentwerts.
    /// </summary>
    private void OnWorkdayToggled(WorkdayToggleViewModel workday)
    {
        _schedule.SetDayHours(workday.Day, workday.IsSelected ? _targetHours : 0);
        OnPropertyChanged(nameof(WorkdayCountText));

        SaveSchedule();
        DisplaySettingsChanged?.Invoke();
    }

    /// <summary>
    /// Schreibt die eine lebende WorkSchedule-Instanz zurück in die Datei. Geladen wird unmittelbar
    /// davor, damit nebenher geänderte Felder nicht überschrieben werden.
    /// </summary>
    private void SaveSchedule()
    {
        var settings = _settingsStore.Load();
        settings.Schedule = _schedule;
        _settingsStore.Save(settings);
    }

    // ===================== Erinnerung =====================

    /// <summary>
    /// Merkt nur die Wahl. Eine tatsächliche Benachrichtigung ist bewusst nicht gebaut: Sie liefe
    /// ohnehin nur, solange die App offen ist, und darüber hat der Auftraggeber noch nicht entschieden.
    /// </summary>
    [ObservableProperty]
    private bool _reminderEnabled;

    partial void OnReminderEnabledChanged(bool value)
    {
        var settings = _settingsStore.Load();
        settings.ReminderEnabled = value;
        _settingsStore.Save(settings);
    }

    // ===================== Navigation =====================

    /// <summary>„Fertig“ und der Zurück-Pfeil: nur zurück zur Übersicht, gespeichert ist längst.</summary>
    public event Action? CloseRequested;

    /// <summary>Abmelden; die Rückfrage davor liegt in der Ansicht.</summary>
    public event Action? LogoutRequested;

    /// <summary>
    /// Etwas hat sich geändert, das die Übersicht neu zeichnen muss: Zeitformat oder Sollstunden.
    /// </summary>
    public event Action? DisplaySettingsChanged;

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    [RelayCommand]
    private void Logout() => LogoutRequested?.Invoke();

    private static string StripScheme(string url)
    {
        foreach (var scheme in new[] { "https://", "http://" })
        {
            if (url.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            {
                return url[scheme.Length..];
            }
        }

        return url;
    }
}
