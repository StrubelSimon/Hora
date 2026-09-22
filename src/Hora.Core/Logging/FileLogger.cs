using System.Globalization;
using Hora.Core.Storage;

namespace Hora.Core.Logging;

/// <summary>
/// Hängt Zeilen an hora.log an. Wird die Datei größer als 1 MB, wird sie beim nächsten Start geleert.
/// Niemals Token oder Authorization-Header hier hineinschreiben.
/// </summary>
public sealed class FileLogger : IAppLogger
{
    private const long MaxSizeBytes = 1_000_000;

    private readonly string _path;
    private readonly object _gate = new();
    private bool _isAvailable = true;

    public FileLogger(AppPaths paths)
    {
        _path = paths.LogFile;
        try
        {
            paths.EnsureRootExists();
            ClearIfTooLarge();
        }
        catch
        {
            // Falls das Verzeichnis nicht erstellt oder die Datei nicht bereinigt werden kann,
            // wird der Logger zur Attrappe: Info und Error tun nichts, aber werfen nicht.
            _isAvailable = false;
        }
    }

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception? exception = null)
    {
        var text = exception is null
            ? message
            : $"{message} | {exception.GetType().Name}: {exception.Message}";
        Write("ERROR", text);
    }

    private void Write(string level, string message)
    {
        if (!_isAvailable)
        {
            return;
        }

        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
        var line = $"{timestamp} [{level}] {message}{Environment.NewLine}";

        lock (_gate)
        {
            try
            {
                File.AppendAllText(_path, line);
            }
            catch (Exception)
            {
                // Protokollieren darf die App niemals zum Absturz bringen.
                // Ein verlorener Protokolleintrag ist harmloser als eine abstürzende App.
                // Das fängt IOException, UnauthorizedAccessException und alle anderen Fehler.
            }
        }
    }

    private void ClearIfTooLarge()
    {
        try
        {
            var info = new FileInfo(_path);
            if (info.Exists && info.Length > MaxSizeBytes)
            {
                File.WriteAllText(_path, string.Empty);
            }
        }
        catch (Exception)
        {
            // Protokollfehler beim Initialisieren werden ignoriert.
            // Ein verlorener Protokolleintrag ist harmloser als eine abstürzende App.
        }
    }
}
