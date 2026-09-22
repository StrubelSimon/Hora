using Hora.Core.Storage;

namespace Hora.Core.Tests.Storage;

public class DpapiAsanaCredentialStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HoraAsanaTests", Guid.NewGuid().ToString("N"));

    private AppPaths Paths => new(_root);

    private static AsanaCredentials Sample() =>
        new("secret-token", "111", "Simon Strubel", "900", "ADVISORI");

    [Fact]
    public void LoadReturnsNullWhenNothingWasSaved()
    {
        Assert.Null(new DpapiAsanaCredentialStore(Paths).Load());
    }

    [Fact]
    public void SaveThenLoadRoundtripsEveryField()
    {
        var store = new DpapiAsanaCredentialStore(Paths);

        store.Save(Sample());
        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal("secret-token", loaded!.Token);
        Assert.Equal("111", loaded.UserGid);
        Assert.Equal("Simon Strubel", loaded.UserName);
        Assert.Equal("900", loaded.WorkspaceGid);
        Assert.Equal("ADVISORI", loaded.WorkspaceName);
    }

    [Fact]
    public void SavedFileIsNotPlainText()
    {
        var store = new DpapiAsanaCredentialStore(Paths);

        store.Save(Sample());

        var bytes = File.ReadAllBytes(Paths.AsanaCredentialsFile);
        Assert.DoesNotContain("secret-token", System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void DeleteRemovesTheFileAndLoadReturnsNullAfterwards()
    {
        var store = new DpapiAsanaCredentialStore(Paths);
        store.Save(Sample());

        store.Delete();

        Assert.False(File.Exists(Paths.AsanaCredentialsFile));
        Assert.Null(store.Load());
    }

    [Fact]
    public void DeleteOnMissingFileDoesNotThrow()
    {
        new DpapiAsanaCredentialStore(Paths).Delete();
    }

    /// <summary>Unlesbar bedeutet „nicht vorhanden" — derselbe Vertrag wie bei DpapiCredentialStore.</summary>
    [Fact]
    public void CorruptedFileIsTreatedAsMissingAndRemoved()
    {
        var store = new DpapiAsanaCredentialStore(Paths);
        store.Save(Sample());
        File.WriteAllBytes(Paths.AsanaCredentialsFile, new byte[] { 1, 2, 3, 4, 5 });

        Assert.Null(store.Load());
        Assert.False(File.Exists(Paths.AsanaCredentialsFile));
    }

    /// <summary>Ein leerer Token ist so wertlos wie gar keiner und darf keine Verbindung vortäuschen.</summary>
    [Fact]
    public void EmptyTokenIsTreatedAsMissing()
    {
        var store = new DpapiAsanaCredentialStore(Paths);
        store.Save(new AsanaCredentials(string.Empty, "111", "Simon", "900", "ADVISORI"));

        Assert.Null(store.Load());
    }

    /// <summary>Asana und Jira liegen in getrennten Dateien; das eine zu löschen darf das andere nicht anfassen.</summary>
    [Fact]
    public void AsanaAndJiraUseSeparateFiles()
    {
        var paths = Paths;
        var jira = new DpapiCredentialStore(paths);
        var asana = new DpapiAsanaCredentialStore(paths);
        jira.Save(new StoredCredentials("https://example.atlassian.net", "a@b.de", "jira-token"));
        asana.Save(Sample());

        asana.Delete();

        Assert.NotNull(jira.Load());
        Assert.NotEqual(paths.CredentialsFile, paths.AsanaCredentialsFile);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Aufräumen darf den Test nicht zum Kippen bringen.
        }
    }
}
