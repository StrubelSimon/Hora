using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hora.App.Services;
using Hora.Core.Worklogs;

namespace Hora.App.ViewModels;

/// <summary>
/// Kalendergitter der Monatsansicht (Abschnitt 3.6 der Gestaltungsvorgaben): das sichtbare Rechteck
/// aus vollen Kalenderwochen, gruppiert in Wochenzeilen zu je sieben Tagen.
/// </summary>
public partial class MonthViewModel : ObservableObject
{
    private readonly WorkSchedule _schedule;

    public MonthViewModel(WorkSchedule schedule, DurationFormat durationFormat = DurationFormat.HoursMinutes)
    {
        _schedule = schedule;
        DurationFormat = durationFormat;
    }

    /// <summary>Darstellung aller Dauern dieser Ansicht, siehe DayViewModel.</summary>
    public DurationFormat DurationFormat { get; set; }

    public ObservableCollection<MonthWeekRow> Weeks { get; } = new();

    public ObservableCollection<IssueTotalRow> IssueTotals { get; } = new();

    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>Nutzer hat eine Tageszelle innerhalb des Monats angeklickt.</summary>
    public event Action<DateOnly>? DaySelected;

    /// <summary>
    /// <paramref name="visibleDays"/> ist das sichtbare Rechteck (Montag der Woche des Monatsersten bis
    /// Sonntag der Woche des Monatsletzten), in der Reihenfolge Montag bis Sonntag je Woche, als
    /// Vielfaches von sieben. <paramref name="issueTotals"/> gilt dagegen nur für den Kalendermonat um
    /// <paramref name="monthAnchor"/>: Kennzahlen zählen für den angezeigten Monat, nicht für das
    /// mitgeladene Rechteck (Abschnitt 6 des Tasks).
    /// </summary>
    public void Show(
        IReadOnlyList<DaySummary> visibleDays,
        DateOnly monthAnchor,
        DateOnly today,
        IReadOnlyList<IssueTotal> issueTotals)
    {
        Weeks.Clear();
        for (var offset = 0; offset < visibleDays.Count; offset += 7)
        {
            var weekDays = visibleDays.Skip(offset).Take(7).ToList();
            var cells = weekDays.Select(day => MonthCellRow.From(day, _schedule, monthAnchor, today, DurationFormat)).ToList();
            var weekTotal = weekDays.Aggregate(TimeSpan.Zero, (sum, day) => sum + day.Total);

            Weeks.Add(new MonthWeekRow(
                WeekCalendar.IsoWeekNumber(weekDays[0].Date),
                DurationFormatter.Format(weekTotal, DurationFormat),
                cells));
        }

        // Der Balken je Ticket bemisst sich am größten Eintrag des Zeitraums (Abschnitt 3.7).
        var maxIssueTotal = IssueTotalRow.MaxTotalOf(issueTotals);
        IssueTotals.Clear();
        foreach (var total in issueTotals)
        {
            IssueTotals.Add(IssueTotalRow.From(total, maxIssueTotal, DurationFormat));
        }

        IsEmpty = issueTotals.Count == 0;
    }

    [RelayCommand]
    private void SelectDay(MonthCellRow? row)
    {
        if (row is { IsActive: true })
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
