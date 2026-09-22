namespace Hora.App.Services;

/// <summary>
/// Die Wahl des Nutzers auf der Profilseite. Gespeichert wird genau diese Wahl, nicht das wirksame
/// Ergebnis: „System“ bleibt „System“, gleich ob Windows gerade hell oder dunkel steht.
/// </summary>
public enum ThemePreference
{
    /// <summary>Folgt der Windows-Einstellung für Apps, auch während die App läuft.</summary>
    System,

    Light,

    Dark,
}

/// <summary>
/// Übersetzt zwischen <see cref="ThemePreference"/> und dem Namen, der in settings.json steht.
/// Unbekanntes — auch null und leer — ergibt hell; eine von Hand verdorbene Datei darf den Start
/// nicht kosten. „Light“ und „Dark“ sind dieselben Namen, die frühere Fassungen geschrieben haben,
/// eine bestehende Datei bleibt also unverändert gültig.
/// </summary>
public static class ThemePreferenceNames
{
    public static ThemePreference Parse(string? storedName) =>
        Enum.TryParse<ThemePreference>(storedName, ignoreCase: true, out var preference)
            && Enum.IsDefined(preference)
                ? preference
                : ThemePreference.Light;

    public static string NameOf(ThemePreference preference) => preference.ToString();
}
