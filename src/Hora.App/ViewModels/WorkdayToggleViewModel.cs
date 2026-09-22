using CommunityToolkit.Mvvm.ComponentModel;

namespace Hora.App.ViewModels;

/// <summary>
/// Einer der sieben quadratischen Umschalter der Zeile „Arbeitstage“. Er trägt nur seine
/// Beschriftung und seinen Zustand; was beim Umschalten geschieht, entscheidet die Profilseite.
/// </summary>
public sealed partial class WorkdayToggleViewModel : ObservableObject
{
    private readonly Action<WorkdayToggleViewModel>? _toggled;

    public WorkdayToggleViewModel(
        DayOfWeek day,
        string label,
        bool isSelected,
        Action<WorkdayToggleViewModel>? toggled = null)
    {
        Day = day;
        Label = label;

        // Feld statt Eigenschaft: Das Übernehmen des gespeicherten Standes ist keine Änderung des
        // Nutzers und darf deshalb weder speichern noch die Übersicht neu zeichnen lassen.
        _isSelected = isSelected;
        _toggled = toggled;
    }

    public DayOfWeek Day { get; }

    /// <summary>„Mo“ bis „So“ — die Beschriftung des Umschalters.</summary>
    public string Label { get; }

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value) => _toggled?.Invoke(this);
}
