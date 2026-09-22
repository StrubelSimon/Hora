using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hora.App.Services;
using Hora.Core.Worklogs;

namespace Hora.App.ViewModels;

public partial class WeekViewModel : ObservableObject
{
    private readonly string _baseUrl;
    private readonly WorkSchedule _schedule;

    public WeekViewModel(string baseUrl, WorkSchedule schedule, DurationFormat durationFormat = DurationFormat.HoursMinutes)
    {
        _baseUrl = baseUrl;
        _schedule = schedule;
        DurationFormat = durationFormat;
    }

    /// <summary>Darstellung aller Dauern dieser Ansicht, siehe DayViewModel.</summary>
    public DurationFormat DurationFormat { get; set; }

    public ObservableCollection<WeekDayRow> Days { get; } = new();

    public ObservableCollection<IssueTotalRow> IssueTotals { get; } = new();

    [ObservableProperty]
    private string _totalText = "0m";

    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>Nutzer hat eine Tageszeile angeklickt.</summary>
    public event Action<DateOnly>? DaySelected;

    public void Show(WeekSummary summary, IReadOnlyList<IssueTotal> issueTotals, DateOnly today)
    {
        // Für Tage ohne Sollzeit (Wochenende) bemisst sich der Balken am größten Tageswert der Woche.
        var weekMaxTotal = summary.Days.Count == 0
            ? TimeSpan.Zero
            : summary.Days.Max(day => day.Total);

        Days.Clear();
        foreach (var day in summary.Days)
        {
            if (!IsVisible(day))
            {
                continue;
            }

            var dailyTarget = _schedule.HoursFor(day.Date.DayOfWeek);
            Days.Add(WeekDayRow.From(day, today, dailyTarget, weekMaxTotal, DurationFormat));
        }

        // Der Balken je Ticket bemisst sich am größten Eintrag des Zeitraums (Abschnitt 3.7).
        var maxIssueTotal = IssueTotalRow.MaxTotalOf(issueTotals);
        IssueTotals.Clear();
        foreach (var total in issueTotals)
        {
            IssueTotals.Add(IssueTotalRow.From(total, _baseUrl, maxIssueTotal, DurationFormat));
        }

        TotalText = DurationFormatter.Format(summary.Total, DurationFormat);
        IsEmpty = summary.Days.All(day => day.Entries.Count == 0);
    }

    /// <summary>
    /// Die Woche zeigt nur die unter „Arbeitstage“ ausgewählten Wochentage — wer Montag bis Freitag
    /// eingestellt hat, bekommt keine Zeile für Samstag und Sonntag zu sehen.
    /// </summary>
    /// <remarks>
    /// Die eine Ausnahme ist ein abgewählter Tag, an dem tatsächlich gebucht wurde: Seine Zeit steckt
    /// in der Wochensumme und in „Pro Ticket“. Ihn trotzdem wegzulassen hieße, gebuchte Zeit
    /// stillschweigend verschwinden zu lassen — die Summe über der Liste stimmte dann mit keiner
    /// sichtbaren Zeile mehr überein.
    /// </remarks>
    private bool IsVisible(DaySummary day) =>
        _schedule.IsWorkday(day.Date.DayOfWeek) || day.Entries.Count > 0;

    [RelayCommand]
    private void SelectDay(WeekDayRow? row)
    {
        if (row is not null)
        {
            DaySelected?.Invoke(row.Date);
        }
    }

    [RelayCommand]
    private void OpenIssue(string? issueUrl)
    {
        if (!string.IsNullOrEmpty(issueUrl))
        {
            BrowserLauncher.Open(issueUrl);
        }
    }
}
