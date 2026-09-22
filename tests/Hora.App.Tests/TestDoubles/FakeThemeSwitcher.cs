using Hora.App.Services;

namespace Hora.App.Tests.TestDoubles;

/// <summary>
/// Attrappe für <see cref="IThemeSwitcher"/>. Der echte ThemeManager tauscht ein
/// ResourceDictionary aus und braucht dafür einen STA-Thread; diese Attrappe merkt sich nur den
/// Zustand und meldet ihn wie das Original über <see cref="Changed"/>.
/// </summary>
/// <remarks>
/// Seit der Dreifach-Wahl hält sie zusätzlich die gespeicherte Wahl (<see cref="Preference"/>) und
/// eine gestellte Windows-Einstellung (<see cref="SystemTheme"/>), damit Tests „System“ auflösen
/// können, ohne die Registrierung anzufassen.
/// </remarks>
internal sealed class FakeThemeSwitcher : IThemeSwitcher
{
    public FakeThemeSwitcher(AppTheme systemTheme = AppTheme.Light)
    {
        SystemTheme = systemTheme;
    }

    /// <summary>Was die Windows-Einstellung für Apps gerade sagt.</summary>
    public AppTheme SystemTheme { get; private set; }

    public AppTheme Current { get; private set; } = AppTheme.Light;

    public ThemePreference Preference { get; private set; } = ThemePreference.Light;

    /// <summary>Zählt jedes Anwenden, gleich über welchen der beiden Wege es kam.</summary>
    public int ApplyCalls { get; private set; }

    public event Action<AppTheme>? Changed;

    public void ApplyPreference(ThemePreference preference)
    {
        ApplyCalls++;
        Preference = preference;
        Current = Resolve(preference);
        Changed?.Invoke(Current);
    }

    public void Apply(AppTheme theme) =>
        ApplyPreference(theme == AppTheme.Dark ? ThemePreference.Dark : ThemePreference.Light);

    public void Toggle() => Apply(Current == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);

    /// <summary>
    /// Der Nutzer stellt die Windows-Einstellung um. Wirksam wird das nur, solange „System“
    /// gewählt ist; die Wahl selbst bleibt dabei unangetastet.
    /// </summary>
    public void SetSystemTheme(AppTheme theme)
    {
        SystemTheme = theme;
        if (Preference != ThemePreference.System || Current == theme)
        {
            return;
        }

        Current = theme;
        Changed?.Invoke(theme);
    }

    private AppTheme Resolve(ThemePreference preference) => preference switch
    {
        ThemePreference.System => SystemTheme,
        ThemePreference.Dark => AppTheme.Dark,
        _ => AppTheme.Light,
    };
}
