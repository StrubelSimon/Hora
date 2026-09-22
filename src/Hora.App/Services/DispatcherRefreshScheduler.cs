using System.Windows.Threading;
using Hora.Core.Logging;

namespace Hora.App.Services;

/// <summary>
/// Der echte Takt: ein <see cref="DispatcherTimer"/>. Er schlägt auf dem Oberflächen-Thread, der
/// ausgelöste Abruf darf die Sammlungen der Ansichten deshalb unmittelbar verändern.
/// </summary>
public sealed class DispatcherRefreshScheduler : IRefreshScheduler
{
    private readonly DispatcherTimer _timer = new();
    private readonly IAppLogger _logger;

    private Func<Task>? _onElapsed;

    public DispatcherRefreshScheduler(IAppLogger logger)
    {
        _logger = logger;
        _timer.Tick += OnTick;
    }

    public void Start(TimeSpan interval, Func<Task> onElapsed)
    {
        _timer.Stop();
        _onElapsed = onElapsed;
        _timer.Interval = interval;
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        _onElapsed = null;
    }

    /// <summary>
    /// „async void“ ist hier die Grenze zum Ereignis und nicht zu vermeiden: Ein Tick-Behandler muss
    /// void sein. Deshalb fängt er alles — eine Ausnahme, die hier entkäme, landete unbehandelt in
    /// der Anwendung und beendete sie. Der Abruf selbst behandelt seine Fehler bereits; dieser
    /// Fangarm ist die zweite Reihe.
    /// </summary>
    private async void OnTick(object? sender, EventArgs e)
    {
        if (_onElapsed is not { } handler)
        {
            return;
        }

        try
        {
            await handler();
        }
        catch (Exception ex)
        {
            _logger.Error("Automatische Aktualisierung fehlgeschlagen", ex);
        }
    }
}
