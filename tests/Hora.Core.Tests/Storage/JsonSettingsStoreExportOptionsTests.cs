using Hora.Core.Storage;

namespace Hora.Core.Tests.Storage;

/// <summary>
/// Die beiden Schalter des Export-Abschnitts in settings.json. Beide stehen auf „an“, wenn die
/// Datei sie nicht kennt: Eine Datei aus der Zeit vor dem Export soll den vollständigen Export
/// liefern und nicht stillschweigend die knappste Fassung.
/// </summary>
public sealed class JsonSettingsStoreExportOptionsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HoraTests", Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;
    private readonly JsonSettingsStore _store;

    public JsonSettingsStoreExportOptionsTests()
    {
        _paths = new AppPaths(_root);
        _store = new JsonSettingsStore(_paths);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void New_settings_switch_both_export_options_on()
    {
        var settings = new AppSettings();

        Assert.True(settings.ExportIncludeIssues);
        Assert.True(settings.ExportIncludeTargets);
    }

    [Fact]
    public void An_older_file_without_the_export_fields_keeps_both_switched_on()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(_paths.SettingsFile, """
            {
              "baseUrl": "https://example.atlassian.net",
              "email": "simon@example.com",
              "theme": "Dark"
            }
            """);

        var settings = _store.Load();

        Assert.True(settings.ExportIncludeIssues);
        Assert.True(settings.ExportIncludeTargets);
    }

    [Fact]
    public void Both_switches_survive_a_save_and_load()
    {
        var settings = _store.Load();
        settings.ExportIncludeIssues = false;
        settings.ExportIncludeTargets = false;
        _store.Save(settings);

        var reloaded = _store.Load();

        Assert.False(reloaded.ExportIncludeIssues);
        Assert.False(reloaded.ExportIncludeTargets);
    }
}
