namespace Hora.App.Services;

/// <summary>
/// Fragt den Nutzer, wohin eine Datei geschrieben werden soll. Als Schnittstelle und nicht als
/// direkter Aufruf des WPF-Dialogs: Das ProfileViewModel kennt — wie beim Designwechsler und beim
/// Taktgeber — keinen WPF-Typ und bleibt damit ohne STA-Thread prüfbar.
/// </summary>
public interface ISaveFilePicker
{
    /// <summary>Der gewählte Pfad; null, wenn der Nutzer abgebrochen hat.</summary>
    string? PickPath(string suggestedFileName);
}
