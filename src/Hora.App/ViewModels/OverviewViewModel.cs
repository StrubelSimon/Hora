using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hora.App.Services;
using Hora.Core.Jira;
using Hora.Core.Jira.Models;
using Hora.Core.Logging;
using Hora.Core.Worklogs;

namespace Hora.App.ViewModels;

/// <summary>
/// Rahmen der Übersicht: Zeitraum-Navigation, Umschalten Tag/Woche/Monat, Laden der Buchungen.
/// Geladen wird im Tages- und Wochenmodus die Woche des gewählten Datums, im Monatsmodus das ganze
/// sichtbare Rechteck des Kalendergitters (Montag der Woche des Monatsersten bis Sonntag der Woche
/// des Monatsletzten); Tages- und Wochenansicht sind Filter auf den jeweils geladenen Daten.
/// </summary>
public partial class OverviewViewModel : ObservableObject
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
    private const string DayMonthYearFormat = "d. MMMM yyyy";
    private const string DayMonthFormat = "d. MMMM";

    private readonly IWorklogService _worklogs;
    private readonly TimeZoneInfo _zone;
    private readonly IAppLogger _logger;
    private readonly Func<DateOnly> _today;
    private readonly WorkSchedule _schedule;
    private readonly IRefreshScheduler? _refreshScheduler;

    private int _autoRefreshMinutes;

    private CancellationTokenSource? _loadCancellation;
    private IReadOnlyList<WorklogEntry> _loadedEntries = Array.Empty<WorklogEntry>();

    // Der Zwischenspeicher merkt sich einen Bereich statt nur eines Wochenanfangs: Der Monatsmodus lädt
    // mehr als eine Woche, und ein Wechsel zwischen den Modi soll nur dann neu laden, wenn der jeweils
    // benötigte Bereich nicht bereits vollständig darin enthalten ist.
    private DateOnly? _loadedFrom;
    private DateOnly? _loadedTo;

    public OverviewViewModel(
        IWorklogService worklogs,
        JiraUser user,
        TimeZoneInfo zone,
        IAppLogger logger,
        Func<DateOnly> today,
        WorkSchedule? schedule = null,
        ViewMode initialMode = ViewMode.Day,
        DurationFormat durationFormat = DurationFormat.HoursMinutes,
        IRefreshScheduler? refreshScheduler = null)
    {
        _worklogs = worklogs;
        _zone = zone;
        _logger = logger;
        _today = today;
        _schedule = schedule ?? WorkSchedule.Default();
        _refreshScheduler = refreshScheduler;

        UserDisplayName = user.DisplayName;
        Day = new DayViewModel(zone, durationFormat);
        Week = new WeekViewModel(_schedule, durationFormat);
        Week.DaySelected += ShowDayFor;
        Month = new MonthViewModel(_schedule, durationFormat);
        Month.DaySelected += ShowDayFor;

        // Die Felder direkt setzen, nicht die Eigenschaften: Der Modus soll beim Aufbau weder ein
        // Neuzeichnen noch das Speichern einer Einstellung auslösen. UpdatePeriodLabel() unten
        // erzeugt danach den zum Startmodus passenden Zeitraumtitel.
        _mode = initialMode;
        _durationFormat = durationFormat;
        _selectedDate = today();
        UpdatePeriodLabel();
    }

    public string AppTitle => $"Hora {AppVersion.Current}";

    public string UserDisplayName { get; }

    /// <summary>Initialen für den Avatar in der Kopfzeile (Abschnitt 3.2 der Gestaltungsvorgaben).</summary>
    public string UserInitials => InitialsFormatter.From(UserDisplayName);

    public DayViewModel Day { get; }

    public WeekViewModel Week { get; }

    public MonthViewModel Month { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDayMode))]
    [NotifyPropertyChangedFor(nameof(IsWeekMode))]
    [NotifyPropertyChangedFor(nameof(IsMonthMode))]
    private ViewMode _mode = ViewMode.Day;

    [ObservableProperty]
    private DateOnly _selectedDate;

    /// <summary>
    /// Darstellung aller Dauern. Ein Wechsel auf der Profilseite setzt diese Eigenschaft; die
    /// Übersicht reicht sie an die drei Ansichten weiter und zeichnet neu. Bewusst als Zustand
    /// dieses Ansichtsmodells und nicht als statischer Schalter im Formatierer.
    /// </summary>
    [ObservableProperty]
    private DurationFormat _durationFormat = DurationFormat.HoursMinutes;

    [ObservableProperty]
    private string _periodLabel = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarning))]
    private string? _warningMessage;

    /// <summary>Kennzahlen (erfasst/soll/differenz/Prozent) des angezeigten, nicht des geladenen Zeitraums.</summary>
    [ObservableProperty]
    private PeriodStats _stats = PeriodStats.For(TimeSpan.Zero, TimeSpan.Zero);

    public bool IsDayMode => Mode == ViewMode.Day;

    public bool IsWeekMode => Mode == ViewMode.Week;

    public bool IsMonthMode => Mode == ViewMode.Month;

    public bool HasError => ErrorMessage is not null;

    public bool HasWarning => WarningMessage is not null;

    /// <summary>Nutzer will sich abmelden.</summary>
    public event Action? LogoutRequested;

    /// <summary>Jira hat die gespeicherte Anmeldung abgelehnt (401/403).</summary>
    public event Action? SessionExpired;

    /// <summary>Nutzer hat den Avatar angeklickt und will auf die Profilseite.</summary>
    public event Action? ProfileRequested;

    /// <summary>Ein Abruf bei Jira war erfolgreich; nur dann wird der Zeitpunkt festgehalten.</summary>
    public event Action? SyncSucceeded;

    partial void OnModeChanged(ViewMode value) => UpdatePeriodLabel();

    partial void OnDurationFormatChanged(DurationFormat value)
    {
        Day.DurationFormat = value;
        Week.DurationFormat = value;
        Month.DurationFormat = value;
        RefreshDisplay();
    }

    partial void OnSelectedDateChanged(DateOnly value) => UpdatePeriodLabel();

    [RelayCommand]
    private void ShowDay()
    {
        Mode = ViewMode.Day;
        Render();
    }

    [RelayCommand]
    private void ShowWeek()
    {
        Mode = ViewMode.Week;
        Render();
    }

    [RelayCommand]
    private Task ShowMonth()
    {
        Mode = ViewMode.Month;
        return LoadAsync();
    }

    [RelayCommand]
    private Task PreviousAsync() => MoveAsync(-1);

    [RelayCommand]
    private Task NextAsync() => MoveAsync(+1);

    [RelayCommand]
    private Task TodayAsync()
    {
        SelectedDate = _today();
        return LoadAsync();
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync(forceReload: true);

    [RelayCommand]
    private void ShowProfile() => ProfileRequested?.Invoke();

    [RelayCommand]
    private void Logout()
    {
        CancelRunningLoad();
        LogoutRequested?.Invoke();
    }

    /// <summary>
    /// Zeichnet die drei Ansichten und die Kennzahlen aus dem bereits geladenen Stand neu. Wird
    /// gebraucht, wenn sich nicht die Daten, sondern ihre Darstellung oder die Sollstunden geändert
    /// haben. Ohne geladene Daten ist <c>_loadedEntries</c> leer — dann entspricht der Aufruf genau
    /// dem leeren Stand, den auch DiscardLoadedPeriod erzeugt.
    /// </summary>
    public void RefreshDisplay() => RenderEntries(_loadedEntries);

    // ===================== Automatische Aktualisierung =====================

    /// <summary>
    /// Abstand, in dem die Buchungen von selbst nachgeholt werden, in Minuten; null schaltet das ab.
    /// Ein neuer Wert beginnt den Abstand sofort von vorn. Ohne Taktgeber (Konstruktor) bleibt der
    /// Wert folgenlos — so verhalten sich die Tests der übrigen Abläufe wie bisher.
    /// </summary>
    public int AutoRefreshMinutes
    {
        get => _autoRefreshMinutes;
        set
        {
            _autoRefreshMinutes = value;
            RestartAutoRefresh();
        }
    }

    private void RestartAutoRefresh()
    {
        if (_refreshScheduler is null)
        {
            return;
        }

        if (_autoRefreshMinutes <= 0)
        {
            _refreshScheduler.Stop();
            return;
        }

        _refreshScheduler.Start(TimeSpan.FromMinutes(_autoRefreshMinutes), AutoRefreshAsync);
    }

    /// <summary>
    /// Ein Takt hat geschlagen. Läuft bereits ein Abruf, setzt dieser Takt aus: Zwei Abrufe
    /// übereinander brächten nur doppelte Last, und der zweite bräche den ersten ab.
    /// </summary>
    private Task AutoRefreshAsync() =>
        _loadCancellation is null ? LoadAsync(forceReload: true, silent: true) : Task.CompletedTask;

    /// <summary>Wechselt in die Tagesansicht des angegebenen Tags; innerhalb des geladenen Bereichs ohne neuen Abruf.</summary>
    public void ShowDayFor(DateOnly date)
    {
        SelectedDate = date;
        Mode = ViewMode.Day;
        Render();
    }

    /// <summary>
    /// Holt die Buchungen des benötigten Zeitraums. <paramref name="silent"/> ist der Abruf des
    /// Takts: Er lässt den angezeigten Stand stehen, setzt keinen Ladezustand und meldet einen
    /// Fehlschlag nicht auf dem Schirm. Andernfalls liefe alle paar Minuten die Ansicht leer, der
    /// Balken blinkte, und eine kurze Störung hinterließe eine rote Zeile über Zahlen, die in
    /// Ordnung sind. Eine abgelaufene Anmeldung meldet sich auch still — die muss man sehen.
    /// </summary>
    public async Task LoadAsync(bool forceReload = false, bool silent = false)
    {
        var (from, to) = RequiredRange();
        if (!forceReload && IsRangeLoaded(from, to))
        {
            Render();
            return;
        }

        CancelRunningLoad();
        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;

        if (!silent)
        {
            IsLoading = true;
            ErrorMessage = null;
            WarningMessage = null;

            // Den bisher angezeigten Stand verwerfen, bevor der Abruf beginnt: PeriodLabel steht bereits
            // auf dem neuen Zeitraum, Stats und die drei Ansichten hingen bis hierher am alten. Ohne
            // dieses Leeren stünden während des Ladens — und nach einem gescheiterten Abruf dauerhaft —
            // die Zahlen und Buchungen des alten Zeitraums unter dem Titel des neuen (Befund B).
            DiscardLoadedPeriod();
        }

        try
        {
            var result = await _worklogs.GetEntriesAsync(from, to, cancellation.Token);
            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            _loadedEntries = result.Entries;
            _loadedFrom = from;
            _loadedTo = to;
            WarningMessage = result.FailedIssueKeys.Count == 0
                ? null
                : $"Für {result.FailedIssueKeys.Count} Ticket(s) konnten die Buchungen nicht geladen werden: {string.Join(", ", result.FailedIssueKeys)}";

            // Ein geglückter Abruf räumt die Meldung eines gescheiterten ab. Beim stillen Abruf ist
            // das die einzige Stelle, an der das geschieht: Zu Beginn zu leeren hieße, eine
            // berechtigte Meldung zu verwerfen, falls auch dieser Versuch scheitert.
            ErrorMessage = null;

            Render();

            // Erst nach dem Rendern und nur im Erfolgsfall: Der Zeitpunkt des letzten erfolgreichen
            // Abrufs darf ein gescheiterter Versuch nicht fortschreiben.
            SyncSucceeded?.Invoke();

            // Der Abstand beginnt nach jedem geglückten Abruf von vorn — auch nach einem von Hand.
            // Sonst käme unmittelbar nach dem Klick auf „Aktualisieren“ noch der Takt hinterher.
            RestartAutoRefresh();
        }
        catch (OperationCanceledException)
        {
            // Nutzer hat weiternavigiert; der alte Abruf ist nicht mehr relevant.
        }
        catch (JiraAuthenticationException)
        {
            SessionExpired?.Invoke();
        }
        catch (JiraRateLimitException ex)
        {
            if (silent)
            {
                _logger.Error("Automatische Aktualisierung: Jira begrenzt gerade die Anfragen", ex);
            }
            else
            {
                ErrorMessage = "Jira begrenzt gerade die Anfragen. Bitte in einer Minute erneut versuchen.";
            }
        }
        catch (JiraException ex)
        {
            _logger.Error("Laden der Buchungen fehlgeschlagen", ex);
            if (!silent)
            {
                ErrorMessage = "Jira ist nicht erreichbar. Netzwerkverbindung prüfen.";
            }
        }
        catch (Exception ex)
        {
            // Unerwartete Ausnahmen dürfen nicht lautlos in einem nicht abgewarteten Befehl
            // verschwinden (Fix-Runde 1, Befund 2): protokollieren und sichtbar machen.
            _logger.Error("Laden der Buchungen unerwartet fehlgeschlagen", ex);
            if (!silent)
            {
                ErrorMessage = "Es ist ein unerwarteter Fehler aufgetreten. Bitte erneut versuchen; Einzelheiten stehen im Protokoll.";
            }
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, cancellation))
            {
                IsLoading = false;
                _loadCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private Task MoveAsync(int direction)
    {
        if (Mode == ViewMode.Month)
        {
            // Auf den Monatsersten springen, statt den Kalendertag fortzuschreiben: Sonst würde ein
            // 31. beim Weiterblättern in einen kürzeren Monat hinein auf dessen letzten Tag gekappt und
            // bliebe dort für alle folgenden Monate hängen (Task R3, Punkt 5).
            var firstOfMonth = new DateOnly(SelectedDate.Year, SelectedDate.Month, 1);
            SelectedDate = firstOfMonth.AddMonths(direction);
        }
        else
        {
            var stepInDays = Mode == ViewMode.Day ? 1 : 7;
            SelectedDate = SelectedDate.AddDays(direction * stepInDays);
        }

        return LoadAsync();
    }

    private void Render()
    {
        var (from, to) = RequiredRange();
        if (!IsRangeLoaded(from, to))
        {
            return; // Für diesen Zeitraum liegen noch keine Daten vor.
        }

        RenderEntries(_loadedEntries);
    }

    /// <summary>
    /// Verwirft den geladenen Zeitraum und zeigt den leeren Stand. Aufgerufen zu Beginn jedes echten
    /// Abrufs: Danach gehören Titel und Zahlen wieder zusammen, auch wenn der Abruf scheitert und
    /// deshalb nie ein Render() mit neuen Daten folgt.
    /// </summary>
    private void DiscardLoadedPeriod()
    {
        _loadedEntries = Array.Empty<WorklogEntry>();
        _loadedFrom = null;
        _loadedTo = null;
        RenderEntries(Array.Empty<WorklogEntry>());
    }

    private void RenderEntries(IReadOnlyList<WorklogEntry> entries)
    {
        var (weekStart, weekEnd) = WeekRange(SelectedDate);
        var (monthStart, monthEnd) = MonthBounds(SelectedDate);
        var (rectFrom, rectTo) = MonthRange(SelectedDate);

        var day = WorklogAggregator.BuildDaySummary(entries, SelectedDate, _zone);
        Day.Show(day, WorklogAggregator.BuildIssueTotals(day.Entries));

        var week = WorklogAggregator.BuildWeekSummary(entries, weekStart, _zone);
        var weekEntries = EntriesInRange(entries, weekStart, weekEnd);
        Week.Show(week, WorklogAggregator.BuildIssueTotals(weekEntries), _today());

        var visibleDays = new List<DaySummary>();
        for (var date = rectFrom; date <= rectTo; date = date.AddDays(1))
        {
            visibleDays.Add(WorklogAggregator.BuildDaySummary(entries, date, _zone));
        }

        var monthEntries = EntriesInRange(entries, monthStart, monthEnd);
        Month.Show(visibleDays, SelectedDate, _today(), WorklogAggregator.BuildIssueTotals(monthEntries));

        // Die Kennzahlen gelten für den angezeigten Zeitraum, nicht für den geladenen: im Monatsmodus
        // ist das der Kalendermonat, nicht das mitgeladene sichtbare Rechteck (Task R3, Punkt 6).
        Stats = Mode switch
        {
            ViewMode.Day => PeriodStats.For(day.Total, TargetCalculator.ForRange(_schedule, SelectedDate, SelectedDate), DurationFormat),
            ViewMode.Week => PeriodStats.For(week.Total, TargetCalculator.ForRange(_schedule, weekStart, weekEnd), DurationFormat),
            ViewMode.Month => PeriodStats.For(SumDuration(monthEntries), TargetCalculator.ForRange(_schedule, monthStart, monthEnd), DurationFormat),
            _ => PeriodStats.For(TimeSpan.Zero, TimeSpan.Zero, DurationFormat),
        };
    }

    private void UpdatePeriodLabel() => PeriodLabel = Mode switch
    {
        ViewMode.Day => SelectedDate.ToString(DayMonthYearFormat, German),
        ViewMode.Week => WeekPeriodLabel(SelectedDate),
        ViewMode.Month => SelectedDate.ToString("MMMM yyyy", German),
        _ => string.Empty,
    };

    private static string WeekPeriodLabel(DateOnly anyDayInWeek)
    {
        var (start, end) = WeekRange(anyDayInWeek);

        if (start.Year == end.Year && start.Month == end.Month)
        {
            return $"{start.Day}. – {end.ToString(DayMonthYearFormat, German)}";
        }

        return start.Year == end.Year
            ? $"{start.ToString(DayMonthFormat, German)} – {end.ToString(DayMonthYearFormat, German)}"
            : $"{start.ToString(DayMonthYearFormat, German)} – {end.ToString(DayMonthYearFormat, German)}";
    }

    /// <summary>Benötigter Bereich für den aktuellen Modus: Tag/Woche die Woche, Monat das sichtbare Rechteck.</summary>
    private (DateOnly From, DateOnly To) RequiredRange() =>
        Mode == ViewMode.Month ? MonthRange(SelectedDate) : WeekRange(SelectedDate);

    private bool IsRangeLoaded(DateOnly from, DateOnly to) =>
        _loadedFrom is { } loadedFrom && _loadedTo is { } loadedTo && loadedFrom <= from && to <= loadedTo;

    private static (DateOnly From, DateOnly To) WeekRange(DateOnly anyDayInWeek)
    {
        var start = WeekCalendar.StartOfWeek(anyDayInWeek);
        return (start, start.AddDays(6));
    }

    private static (DateOnly First, DateOnly Last) MonthBounds(DateOnly anyDayInMonth)
    {
        var first = new DateOnly(anyDayInMonth.Year, anyDayInMonth.Month, 1);
        return (first, first.AddMonths(1).AddDays(-1));
    }

    /// <summary>Montag der Woche des Monatsersten bis Sonntag der Woche des Monatsletzten: das sichtbare Rechteck.</summary>
    private static (DateOnly From, DateOnly To) MonthRange(DateOnly anyDayInMonth)
    {
        var (first, last) = MonthBounds(anyDayInMonth);
        return (WeekCalendar.StartOfWeek(first), WeekCalendar.StartOfWeek(last).AddDays(6));
    }

    private IReadOnlyList<WorklogEntry> EntriesInRange(IEnumerable<WorklogEntry> entries, DateOnly from, DateOnly to) =>
        entries.Where(entry => entry.LocalDate(_zone) >= from && entry.LocalDate(_zone) <= to).ToList();

    private static TimeSpan SumDuration(IEnumerable<WorklogEntry> entries) =>
        entries.Aggregate(TimeSpan.Zero, (total, entry) => total + entry.Duration);

    private void CancelRunningLoad()
    {
        // Das Feld bewusst nicht auf null setzen: Der finally-Block eines noch laufenden
        // Abrufs erkennt anhand von ReferenceEquals, ob er noch der aktuelle Abruf ist, und
        // muss dafür weiterhin sein eigenes Token in _loadCancellation vorfinden. Ein Aufruf
        // von LoadAsync überschreibt das Feld direkt danach ohnehin mit einem neuen Token;
        // ruft dagegen Logout() diese Methode auf, bleibt so sichergestellt, dass der laufende
        // Abruf beim Abbrechen seinen eigenen Ladezustand zurücksetzt (Task 12, Fix-Runde 1).
        _loadCancellation?.Cancel();
    }
}
