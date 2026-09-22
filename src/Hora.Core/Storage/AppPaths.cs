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

    public string SettingsFile => Path.Combine(RootDirectory, "settings.json");

    public string LogFile => Path.Combine(RootDirectory, "hora.log");

    public void EnsureRootExists() => Directory.CreateDirectory(RootDirectory);
}
