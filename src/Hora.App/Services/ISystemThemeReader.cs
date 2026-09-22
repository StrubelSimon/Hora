namespace Hora.App.Services;

/// <summary>
/// Liest die Windows-Einstellung für Apps (hell oder dunkel) und meldet ihre Änderungen.
/// </summary>
/// <remarks>
/// Die Abstraktion sitzt hier, damit die Tests ohne echte Registrierung auskommen: Ein Test, der
/// „System“ auf dunkel auflösen will, dürfte sonst die Windows-Einstellung des Auftraggebers
/// umstellen. Die einzige echte Umsetzung ist <see cref="RegistrySystemThemeReader"/>.
/// </remarks>
public interface ISystemThemeReader
{
    /// <summary>Was Windows gerade sagt. Fehlt der Wert oder ist er nicht lesbar, gilt hell.</summary>
    AppTheme Current { get; }

    /// <summary>Meldet ausschließlich echte Wechsel, nicht jede beliebige Systemänderung.</summary>
    event Action? Changed;
}
