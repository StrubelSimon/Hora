namespace Hora.Core.Storage;

/// <summary>Ablageorte der App-Dateien. Standard ist %LOCALAPPDATA%\Hora.</summary>
public sealed class AppPaths
{
    public AppPaths(string rootDirectory)
    {
        RootDirectory = rootDirectory;
    }

    public static AppPaths Default() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hora"));

    public string RootDirectory { get; }

    public string CredentialsFile => Path.Combine(RootDirectory, "credentials.dat");

    /// <summary>
    /// Eigene Datei für die Everhour-Verbindung. Bewusst nicht in credentials.dat: Die entsteht nur,
    /// wenn „Angemeldet bleiben" gesetzt ist — wer das abwählt, hätte sonst keinen Ort für den
    /// Schlüssel. Getrennte Dateien heißen außerdem: Everhour trennen fasst die Jira-Anmeldung nicht an.
    /// </summary>
    public string EverhourCredentialsFile => Path.Combine(RootDirectory, "everhour.dat");

    /// <summary>Die frühere Asana-Verbindung mit Asana-Token. Wird nur noch gelöscht.</summary>
    public string LegacyAsanaCredentialsFile => Path.Combine(RootDirectory, "asana.dat");

    public string SettingsFile => Path.Combine(RootDirectory, "settings.json");

    public string LogFile => Path.Combine(RootDirectory, "hora.log");

    public void EnsureRootExists() => Directory.CreateDirectory(RootDirectory);
}
