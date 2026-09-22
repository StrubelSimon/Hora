namespace Hora.App.ViewModels;

/// <summary>
/// Übersetzt zwischen <see cref="ViewMode"/> und dem Namen, der in settings.json steht.
/// Unbekanntes — auch null und leer — ergibt die Tagesansicht; eine von Hand verdorbene Datei darf
/// den Start nicht kosten.
/// </summary>
public static class ViewModeNames
{
    public static ViewMode Parse(string? storedName) =>
        Enum.TryParse<ViewMode>(storedName, ignoreCase: true, out var mode) ? mode : ViewMode.Day;

    public static string NameOf(ViewMode mode) => mode.ToString();
}
