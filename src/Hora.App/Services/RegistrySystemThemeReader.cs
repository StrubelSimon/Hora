using Microsoft.Win32;

namespace Hora.App.Services;

/// <summary>
/// Liest die Windows-Einstellung für Apps aus der Registrierung und horcht über
/// <see cref="SystemEvents.UserPreferenceChanged"/> auf ihre Änderungen.
/// </summary>
/// <remarks>
/// <para>
/// Warum SystemEvents und nicht die Fensternachricht WM_SETTINGCHANGE im vorhandenen
/// HwndSource-Hook von MainWindow: Der Hook dort behandelt WM_GETMINMAXINFO und lässt
/// <c>handled</c> bewusst auf false stehen; dort noch eine zweite, fachlich fremde Nachricht
/// auszuwerten, verbände die Designlogik mit dem Fenster. SystemEvents hängt dagegen an keinem
/// Fenster — die Anmeldung überlebt jedes Schließen und Neuöffnen des Hauptfensters, und die
/// Designlogik bleibt in der Dienstschicht, wo sie hinter <see cref="ISystemThemeReader"/> ohne
/// echte Registrierung prüfbar ist. Außerdem trägt WM_SETTINGCHANGE die Information nur als
/// Zeichenkette „ImmersiveColorSet“ hinter einem Zeiger, die erst aus dem nicht verwalteten
/// Speicher gelesen werden müsste.
/// </para>
/// <para>
/// SystemEvents hält eine statische Liste seiner Anmeldungen. Ohne <see cref="Dispose"/> bliebe
/// dieses Objekt für die Lebensdauer des Prozesses am Leben; App.OnExit räumt es deshalb ab.
/// </para>
/// </remarks>
public sealed class RegistrySystemThemeReader : ISystemThemeReader, IDisposable
{
    private const string PersonalizeKey =
        @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    private readonly Action<Action>? _marshalToUi;
    private readonly UserPreferenceChangedEventHandler _handler;
    private bool _disposed;

    /// <param name="marshalToUi">
    /// SystemEvents meldet auf einem eigenen Thread. Wer daraufhin Oberfläche anfasst — der
    /// ThemeManager tauscht ein ResourceDictionary aus — muss zurück auf den Oberflächen-Thread.
    /// App reicht dafür den Dispatcher herein; ohne Angabe wird direkt gemeldet (Tests).
    /// </param>
    public RegistrySystemThemeReader(Action<Action>? marshalToUi = null)
    {
        _marshalToUi = marshalToUi;
        Current = ReadFromRegistry();
        _handler = OnUserPreferenceChanged;
        SystemEvents.UserPreferenceChanged += _handler;
    }

    public AppTheme Current { get; private set; }

    public event Action? Changed;

    /// <summary>
    /// AppsUseLightTheme: 0 bedeutet dunkel, 1 bedeutet hell. Fehlt der Wert oder ist er nicht
    /// lesbar, gilt hell.
    /// </summary>
    public static AppTheme ReadFromRegistry()
    {
        try
        {
            return Registry.GetValue(PersonalizeKey, AppsUseLightThemeValue, null) is int appsUseLightTheme
                && appsUseLightTheme == 0
                    ? AppTheme.Dark
                    : AppTheme.Light;
        }
        catch (Exception)
        {
            // Nicht lesbar heißt hell — dieselbe Zusicherung wie beim fehlenden Wert. Ein Fehler
            // beim Lesen einer Einstellung darf die App nicht kosten.
            return AppTheme.Light;
        }
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        // UserPreferenceChanged meldet auch Schriftarten, Maus und Energie. Der Designwechsel
        // kommt als General; Color und VisualStyle sind mitgenommen, weil das je nach
        // Windows-Fassung schwankt. Teuer ist das nicht: Es folgt nur ein Registrierungszugriff.
        if (e.Category is not (UserPreferenceCategory.General
            or UserPreferenceCategory.Color
            or UserPreferenceCategory.VisualStyle))
        {
            return;
        }

        var next = ReadFromRegistry();
        if (next == Current)
        {
            return; // keine echte Änderung — nichts melden, sonst zeichnete die App grundlos neu
        }

        Current = next;

        if (_marshalToUi is null)
        {
            Changed?.Invoke();
        }
        else
        {
            _marshalToUi(() => Changed?.Invoke());
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.UserPreferenceChanged -= _handler;
    }
}
