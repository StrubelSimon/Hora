using Hora.Core.Storage;

namespace Hora.Core.Tests.Storage;

/// <summary>
/// Das Feld <c>theme</c> in settings.json trägt seit der Dreifach-Wahl auch den Wert „System“.
/// Gespeichert wird die Wahl, nicht das wirksame Ergebnis; sie muss einen Neustart überstehen.
/// </summary>
public sealed class JsonSettingsStoreThemeChoiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HoraTests", Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;
    private readonly JsonSettingsStore _store;

    public JsonSettingsStoreThemeChoiceTests()
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

    [Theory]
    [InlineData("System")]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void The_stored_theme_choice_survives_a_restart(string choice)
    {
        _store.Save(new AppSettings { Theme = choice });

        // Ein zweiter Speicher über denselben Pfad ist der nächste Programmstart.
        var reloaded = new JsonSettingsStore(_paths).Load();

        Assert.Equal(choice, reloaded.Theme);
    }

    [Fact]
    public void The_choice_system_really_stands_in_the_file()
    {
        _store.Save(new AppSettings { Theme = "System" });

        var json = File.ReadAllText(_paths.SettingsFile);

        Assert.Contains("\"theme\": \"System\"", json);
    }

    [Fact]
    public void An_empty_theme_from_a_hand_edited_file_still_falls_back_to_light()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(_paths.SettingsFile, "{ \"theme\": \"\" }");

        Assert.Equal("Light", _store.Load().Theme);
    }

    [Fact]
    public void An_older_file_without_a_theme_is_read_as_light()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(_paths.SettingsFile, "{ \"email\": \"a@b.de\" }");

        Assert.Equal("Light", _store.Load().Theme);
    }
}
