using Microsoft.Win32;

namespace Hora.App.Services;

/// <summary>Der Speichern-Dialog von Windows. Den zuletzt benutzten Ordner merkt er sich selbst.</summary>
public sealed class SaveFileDialogPicker : ISaveFilePicker
{
    public string? PickPath(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            DefaultExt = ".csv",
            Filter = "CSV-Datei (*.csv)|*.csv|Alle Dateien (*.*)|*.*",
            AddExtension = true,
            OverwritePrompt = true,
            Title = "Zeiten exportieren",
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
