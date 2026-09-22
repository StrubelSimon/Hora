using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hora.App.Services;
using Hora.Core.Worklogs;

namespace Hora.App.ViewModels;

public partial class DayViewModel : ObservableObject
{
    private readonly TimeZoneInfo _zone;

    public DayViewModel(TimeZoneInfo zone, DurationFormat durationFormat = DurationFormat.HoursMinutes)
    {
        _zone = zone;
        DurationFormat = durationFormat;
    }

    /// <summary>
    /// Darstellung aller Dauern dieser Ansicht. Wird beim Umschalten auf der Profilseite gesetzt;
    /// danach zeichnet die Übersicht die Zeilen neu, statt einen globalen Schalter umzulegen.
    /// </summary>
    public DurationFormat DurationFormat { get; set; }

    public ObservableCollection<WorklogRow> Rows { get; } = new();

    public ObservableCollection<IssueTotalRow> IssueTotals { get; } = new();

    [ObservableProperty]
    private string _totalText = "0m";

    [ObservableProperty]
    private bool _isEmpty = true;

    public void Show(DaySummary summary, IReadOnlyList<IssueTotal> issueTotals)
    {
        Rows.Clear();
        foreach (var entry in summary.Entries)
        {
            Rows.Add(WorklogRow.From(entry, _zone, DurationFormat));
        }

        // Der Balken je Ticket bemisst sich am größten Eintrag des Zeitraums (Abschnitt 3.7).
        var maxTotal = IssueTotalRow.MaxTotalOf(issueTotals);
        IssueTotals.Clear();
        foreach (var total in issueTotals)
        {
            IssueTotals.Add(IssueTotalRow.From(total, maxTotal, DurationFormat));
        }

        TotalText = DurationFormatter.Format(summary.Total, DurationFormat);
        IsEmpty = summary.Entries.Count == 0;
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
