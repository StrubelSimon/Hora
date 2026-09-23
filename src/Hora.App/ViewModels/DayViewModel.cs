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

    /// <summary>Die Jira-Buchungen des Tages, chronologisch.</summary>
    public ObservableCollection<WorklogRow> Rows { get; } = new();

    /// <summary>
    /// Die Asana-Einträge des Tages. Ein eigener Block, weil sie keine Uhrzeit tragen und in der
    /// chronologischen Liste nur eine Ordnung vortäuschten, die es nicht gibt.
    /// </summary>
    public ObservableCollection<WorklogRow> AsanaRows { get; } = new();

    public ObservableCollection<IssueTotalRow> IssueTotals { get; } = new();

    /// <summary>Trägt der Tag Asana-Zeiten? Daran hängt der ganze Block samt Zwischenüberschrift.</summary>
    [ObservableProperty]
    private bool _hasAsanaRows;

    [ObservableProperty]
    private string _totalText = "0m";

    [ObservableProperty]
    private bool _isEmpty = true;

    public void Show(DaySummary summary, IReadOnlyList<IssueTotal> issueTotals)
    {
        Rows.Clear();
        AsanaRows.Clear();
        foreach (var entry in summary.Entries)
        {
            var row = WorklogRow.From(entry, _zone, DurationFormat);
            if (entry.Source == WorklogSource.Asana)
            {
                AsanaRows.Add(row);
            }
            else
            {
                Rows.Add(row);
            }
        }

        HasAsanaRows = AsanaRows.Count > 0;

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
