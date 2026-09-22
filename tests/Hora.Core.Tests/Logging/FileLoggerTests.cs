using Hora.Core.Logging;
using Hora.Core.Storage;

namespace Hora.Core.Tests.Logging;

public sealed class FileLoggerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HoraTests", Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;

    public FileLoggerTests()
    {
        _paths = new AppPaths(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Info_appends_a_line_with_level_and_message()
    {
        var logger = new FileLogger(_paths);

        logger.Info("App gestartet");

        var line = Assert.Single(File.ReadAllLines(_paths.LogFile));
        Assert.Contains("[INFO]", line);
        Assert.EndsWith("App gestartet", line);
    }

    [Fact]
    public void Error_includes_exception_type_and_message()
    {
        var logger = new FileLogger(_paths);

        logger.Error("Laden fehlgeschlagen", new InvalidOperationException("Grund"));

        var line = Assert.Single(File.ReadAllLines(_paths.LogFile));
        Assert.Contains("[ERROR]", line);
        Assert.Contains("Laden fehlgeschlagen", line);
        Assert.Contains("InvalidOperationException", line);
        Assert.Contains("Grund", line);
    }

    [Fact]
    public void Constructor_clears_log_file_larger_than_one_megabyte()
    {
        _paths.EnsureRootExists();
        File.WriteAllText(_paths.LogFile, new string('x', 1_100_000));

        _ = new FileLogger(_paths);

        Assert.Equal(0, new FileInfo(_paths.LogFile).Length);
    }

    [Fact]
    public void Constructor_keeps_small_log_file()
    {
        _paths.EnsureRootExists();
        File.WriteAllText(_paths.LogFile, "alt" + Environment.NewLine);

        _ = new FileLogger(_paths);

        Assert.Equal("alt" + Environment.NewLine, File.ReadAllText(_paths.LogFile));
    }

    [Fact]
    public void Constructor_does_not_throw_when_root_cannot_be_created()
    {
        // Erstelle eine Datei dort, wo das Verzeichnis sein soll.
        // Directory.CreateDirectory wirft dann, weil dort bereits eine Datei liegt.
        var rootAsFile = Path.Combine(Path.GetTempPath(), "HoraTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(rootAsFile)!);
        File.WriteAllText(rootAsFile, "blocking");

        var blockingPaths = new AppPaths(rootAsFile);

        // Der Konstruktor darf nicht werfen.
        var logger = new FileLogger(blockingPaths);

        // Info und Error dürfen auch nicht werfen.
        logger.Info("test");
        logger.Error("test", new Exception("test"));

        // Cleanup
        File.Delete(rootAsFile);
    }

    [Fact]
    public void Info_does_not_throw_when_log_file_cannot_be_written()
    {
        // Erstelle das Wurzelverzeichnis.
        _paths.EnsureRootExists();

        // Erstelle ein Verzeichnis dort, wo die Log-Datei sein soll.
        // File.AppendAllText scheitert dann.
        Directory.CreateDirectory(_paths.LogFile);

        var logger = new FileLogger(_paths);

        // Info darf nicht werfen, auch wenn die Datei nicht geschrieben werden kann.
        logger.Info("test");
        logger.Error("test", new Exception("test"));

        // Cleanup
        Directory.Delete(_paths.LogFile);
    }
}
