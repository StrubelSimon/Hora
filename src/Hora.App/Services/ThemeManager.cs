using System.Collections.ObjectModel;
using System.Windows;

namespace Hora.App.Services;

public enum AppTheme
{
    Light,
    Dark,
}

/// <summary>
/// Tauscht zur Laufzeit das Wörterbuch mit den Farbwerten aus. Alle Ansichten verweisen über
/// DynamicResource darauf, deshalb genügt der Austausch des Wörterbuchs.
/// </summary>
/// <remarks>
/// Hier liegt auch die Auflösung der Wahl „System“: Der Manager hält die Wahl
/// (<see cref="Preference"/>) und das daraus abgeleitete wirksame Aussehen (<see cref="Current"/>)
/// auseinander. Solange „System“ gewählt ist, folgt er jeder Änderung der Windows-Einstellung; nach
/// einer ausdrücklichen Wahl hört er nicht mehr darauf.
/// </remarks>
public sealed class ThemeManager : IThemeSwitcher, IDisposable
{
    // Assembly-qualifizierte pack-URI statt einer relativen Quellenangabe: Eine relative Quelle
    // löst WPF über Application.ResourceAssembly auf, und die ist im xUnit-Testprozess (anders als
    // beim echten Programmstart) bereits auf testhost statt auf Hora festgelegt und lässt sich kein
    // zweites Mal setzen. Die explizite Form "Hora;component/…" benennt die Assembly unabhängig davon
    // und funktioniert deshalb sowohl im laufenden Programm als auch in ThemeManagerTests zuverlässig.
    private const string LightSource = "pack://application:,,,/Hora;component/Styles/Tokens.Light.xaml";
    private const string DarkSource = "pack://application:,,,/Hora;component/Styles/Tokens.Dark.xaml";

    private readonly Collection<ResourceDictionary> _mergedDictionaries;
    private readonly ISystemThemeReader? _systemTheme;
    private readonly Action? _systemThemeChangedHandler;
    private ResourceDictionary? _currentTokens;
    private bool _disposed;

    public ThemeManager(Collection<ResourceDictionary> mergedDictionaries, ISystemThemeReader? systemTheme = null)
    {
        _mergedDictionaries = mergedDictionaries;
        _systemTheme = systemTheme;

        if (_systemTheme is not null)
        {
            _systemThemeChangedHandler = OnSystemThemeChanged;
            _systemTheme.Changed += _systemThemeChangedHandler;
        }
    }

    /// <summary>Das wirksame Aussehen.</summary>
    public AppTheme Current { get; private set; } = AppTheme.Light;

    /// <summary>Die Wahl des Nutzers. Ohne eigenes Zutun steht sie auf hell, wie bisher.</summary>
    public ThemePreference Preference { get; private set; } = ThemePreference.Light;

    public event Action<AppTheme>? Changed;

    /// <summary>Ausdrückliche Wahl; sie verlässt „System“.</summary>
    public void Apply(AppTheme theme) =>
        ApplyPreference(theme == AppTheme.Dark ? ThemePreference.Dark : ThemePreference.Light);

    public void ApplyPreference(ThemePreference preference)
    {
        Preference = preference;
        ApplyEffective(Resolve(preference));
    }

    public void Toggle() => Apply(Current == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);

    /// <summary>
    /// Ohne Zugriff auf die Windows-Einstellung gilt hell — dieselbe Zusicherung wie bei einem
    /// fehlenden oder unlesbaren Registrierungswert.
    /// </summary>
    private AppTheme Resolve(ThemePreference preference) => preference switch
    {
        ThemePreference.System => _systemTheme?.Current ?? AppTheme.Light,
        ThemePreference.Dark => AppTheme.Dark,
        _ => AppTheme.Light,
    };

    private void ApplyEffective(AppTheme theme)
    {
        var source = new Uri(theme == AppTheme.Dark ? DarkSource : LightSource);
        var next = new ResourceDictionary { Source = source };

        if (_currentTokens is not null)
        {
            _mergedDictionaries.Remove(_currentTokens);
        }

        // Die Token müssen an erster Stelle stehen, damit die Stile in Theme.xaml sie finden.
        _mergedDictionaries.Insert(0, next);
        _currentTokens = next;
        Current = theme;
        Changed?.Invoke(theme);
    }

    /// <summary>
    /// Der Nutzer hat die Windows-Einstellung umgestellt. Nur wer „System“ gewählt hat, folgt ihr;
    /// eine ausdrückliche Wahl bleibt stehen.
    /// </summary>
    private void OnSystemThemeChanged()
    {
        if (Preference != ThemePreference.System)
        {
            return;
        }

        var next = Resolve(ThemePreference.System);
        if (next == Current)
        {
            return;
        }

        ApplyEffective(next);
    }

    /// <summary>
    /// Löst die Anmeldung an der Windows-Einstellung. App.OnExit ruft das auf; ohne das hinge der
    /// Manager über den Leser an der statischen Anmeldungsliste von SystemEvents.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_systemTheme is not null && _systemThemeChangedHandler is not null)
        {
            _systemTheme.Changed -= _systemThemeChangedHandler;
        }
    }
}
