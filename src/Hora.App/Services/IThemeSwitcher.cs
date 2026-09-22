namespace Hora.App.Services;

/// <summary>
/// Was die Ansichtsmodelle vom Designwechsel brauchen. Der echte <see cref="ThemeManager"/> tauscht
/// dafür ein ResourceDictionary aus und braucht einen STA-Thread; hinter dieser Schnittstelle bleiben
/// MainViewModel und ProfileViewModel frei von WPF-Typen und damit ohne STA-Thread prüfbar.
/// </summary>
public interface IThemeSwitcher
{
    /// <summary>Das wirksame Aussehen — bei der Wahl „System“ also das, was Windows gerade sagt.</summary>
    AppTheme Current { get; }

    /// <summary>Die gespeicherte Wahl. Sie ist die Quelle für den Segmentschalter im Profil.</summary>
    ThemePreference Preference { get; }

    /// <summary>Meldet jeden Wechsel des wirksamen Aussehens, gleich woher er kam.</summary>
    event Action<AppTheme>? Changed;

    /// <summary>Setzt eine ausdrückliche Wahl und verlässt damit „System“.</summary>
    void Apply(AppTheme theme);

    /// <summary>Setzt die Wahl; „System“ wird dabei an der Windows-Einstellung aufgelöst.</summary>
    void ApplyPreference(ThemePreference preference);

    /// <summary>
    /// Wechselt auf das jeweils andere Design — das, was die Pille in der Fensterleiste tut. Der
    /// Bezugspunkt ist das wirksame Aussehen; ein Klick setzt deshalb immer einen ausdrücklichen
    /// Wert und verlässt „System“.
    /// </summary>
    void Toggle() => Apply(Current == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);
}
