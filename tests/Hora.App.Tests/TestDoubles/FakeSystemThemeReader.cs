using Hora.App.Services;

namespace Hora.App.Tests.TestDoubles;

/// <summary>
/// Attrappe für <see cref="ISystemThemeReader"/>. Sie ersetzt die Windows-Registrierung: Die Tests
/// stellen die Windows-Einstellung selbst und melden eine Änderung, ohne etwas am Rechner anzufassen.
/// </summary>
internal sealed class FakeSystemThemeReader : ISystemThemeReader
{
    public FakeSystemThemeReader(AppTheme current = AppTheme.Light)
    {
        Current = current;
    }

    public AppTheme Current { get; private set; }

    /// <summary>Zahl der Anmeldungen minus Abmeldungen — so wird ein fehlendes Abmelden sichtbar.</summary>
    public int Listeners { get; private set; }

    private Action? _changed;

    public event Action? Changed
    {
        add
        {
            _changed += value;
            Listeners++;
        }
        remove
        {
            _changed -= value;
            Listeners--;
        }
    }

    /// <summary>Der Nutzer stellt die Windows-Einstellung um; wie das Original meldet das nur echte Wechsel.</summary>
    public void Set(AppTheme theme)
    {
        if (theme == Current)
        {
            return;
        }

        Current = theme;
        _changed?.Invoke();
    }
}
