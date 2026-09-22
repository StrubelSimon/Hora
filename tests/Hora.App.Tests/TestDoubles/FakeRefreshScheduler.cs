using Hora.App.Services;

namespace Hora.App.Tests.TestDoubles;

/// <summary>
/// Attrappe für <see cref="IRefreshScheduler"/>. Der echte Takt hängt an einem DispatcherTimer und
/// bräuchte im Test sowohl einen STA-Thread als auch echte Wartezeit; diese Attrappe merkt sich nur,
/// ob und mit welchem Abstand sie laufen soll, und lässt den Takt über <see cref="TickAsync"/> von
/// Hand schlagen.
/// </summary>
internal sealed class FakeRefreshScheduler : IRefreshScheduler
{
    private Func<Task>? _onElapsed;

    public bool IsRunning { get; private set; }

    /// <summary>Der zuletzt übergebene Abstand; <see cref="TimeSpan.Zero"/>, solange nie gestartet wurde.</summary>
    public TimeSpan Interval { get; private set; }

    /// <summary>Zählt jeden Start — auch einen, der nur den Abstand von vorn beginnen lässt.</summary>
    public int StartCalls { get; private set; }

    public int StopCalls { get; private set; }

    public void Start(TimeSpan interval, Func<Task> onElapsed)
    {
        IsRunning = true;
        Interval = interval;
        _onElapsed = onElapsed;
        StartCalls++;
    }

    public void Stop()
    {
        IsRunning = false;
        StopCalls++;
    }

    /// <summary>
    /// Ein Takt schlägt; die zurückgegebene Aufgabe ist der ausgelöste Abruf, auf den der Test
    /// warten kann. Steht der Takt still, passiert nichts — wie beim echten Timer.
    /// </summary>
    public Task TickAsync() => IsRunning && _onElapsed is { } handler ? handler() : Task.CompletedTask;
}
