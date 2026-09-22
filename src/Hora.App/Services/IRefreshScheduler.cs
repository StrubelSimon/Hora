namespace Hora.App.Services;

/// <summary>
/// Ein wiederkehrender Takt für die automatische Aktualisierung der Übersicht.
/// </summary>
/// <remarks>
/// Eigene Schnittstelle statt eines DispatcherTimer im ViewModel: Der Timer braucht einen
/// Dispatcher und damit einen STA-Thread, und ein Test müsste echte Minuten abwarten. Dasselbe
/// Vorgehen wie bei <see cref="IThemeSwitcher"/>.
///
/// Der Rückruf gibt eine Aufgabe zurück, statt ein Ereignis zu sein: Ein Ereignis ließe sich nicht
/// abwarten, der Takt müsste den Abruf also in einem „async void“ verlieren — und ein Test hätte
/// keinen Punkt, an dem er auf dessen Ende warten kann.
/// </remarks>
public interface IRefreshScheduler
{
    /// <summary>
    /// Beginnt den Abstand von vorn. Ein bereits laufender Takt wird dabei verworfen; der erste
    /// Rückruf kommt also frühestens nach <paramref name="interval"/>.
    /// </summary>
    void Start(TimeSpan interval, Func<Task> onElapsed);

    void Stop();
}
