using Hora.Core.Logging;

namespace Hora.App.Tests.TestDoubles;

/// <summary>
/// Protokoll-Attrappe, die mitschreibt. Gebraucht dort, wo ein Fehler den Nutzer nur eine
/// Statuszeile kostet — dann ist der Protokolleintrag die einzige Spur, und dass es ihn gibt,
/// gehört geprüft.
/// </summary>
internal sealed class RecordingLogger : IAppLogger
{
    public List<string> Infos { get; } = new();

    public List<(string Message, Exception? Exception)> Errors { get; } = new();

    public void Info(string message) => Infos.Add(message);

    public void Error(string message, Exception? exception = null) => Errors.Add((message, exception));
}
