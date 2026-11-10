using Hora.Core.Storage;

namespace Hora.Core.Tests.Storage;

public class DpapiEverhourCredentialStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HoraEverhourTests", Guid.NewGuid().ToString("N"));

    private AppPaths Paths => new(_root);

    private static EverhourCredentials Sample() =>
        new("secret-key", 1431001, "Simon Strubel");

    [Fact]
    public void LoadReturnsNullWhenNothingWasSaved()
    {
        Assert.Null(new DpapiEverhourCredentialStore(Paths).Load());
    }

    [Fact]
    public void SaveThenLoadRoundtripsEveryField()
    {
        var store = new DpapiEverhourCredentialStore(Paths);

        store.Save(Sample());
        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal("secret-key", loaded!.ApiKey);
        Assert.Equal(1431001, loaded.UserId);
        Assert.Equal("Simon Strubel", loaded.UserName);
    }

    [Fact]
    public void SavedFileIsNotPlainText()
    {
        var store = new DpapiEverhourCredentialStore(Paths);

        store.Save(Sample());

        var bytes = File.ReadAllBytes(Paths.EverhourCredentialsFile);
        Assert.DoesNotContain("secret-key", System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void DeleteRemovesTheFileAndLoadReturnsNullAfterwards()
    {
        var store = new DpapiEverhourCredentialStore(Paths);
        store.Save(Sample());

        store.Delete();

        Assert.False(File.Exists(Paths.EverhourCredentialsFile));
        Assert.Null(store.Load());
    }

    [Fact]
    public void DeleteOnMissingFileDoesNotThrow()
    {
        new DpapiEverhourCredentialStore(Paths).Delete();
    }

    /// <summary>Unlesbar bedeutet „nicht vorhanden" — derselbe Vertrag wie bei DpapiCredentialStore.</summary>
    [Fact]
    public void CorruptedFileIsTreatedAsMissingAndRemoved()
    {
        var store = new DpapiEverhourCredentialStore(Paths);
        store.Save(Sample());
        File.WriteAllBytes(Paths.EverhourCredentialsFile, new byte[] { 1, 2, 3, 4, 5 });

        Assert.Null(store.Load());
        Assert.False(File.Exists(Paths.EverhourCredentialsFile));
    }

    /// <summary>Ein leerer Schlüssel ist so wertlos wie gar keiner und darf keine Verbindung vortäuschen.</summary>
    [Fact]
    public void EmptyApiKeyIsTreatedAsMissing()
    {
        var store = new DpapiEverhourCredentialStore(Paths);
        store.Save(new EverhourCredentials(string.Empty, 1431001, "Simon"));

        Assert.Null(store.Load());
    }

    /// <summary>Everhour und Jira liegen in getrennten Dateien; das eine zu löschen darf das andere nicht anfassen.</summary>
    [Fact]
    public void EverhourAndJiraUseSeparateFiles()
    {
        var paths = Paths;
        var jira = new DpapiCredentialStore(paths);
        var everhour = new DpapiEverhourCredentialStore(paths);
        jira.Save(new StoredCredentials("https://example.atlassian.net", "a@b.de", "jira-token"));
        everhour.Save(Sample());

        everhour.Delete();

        Assert.NotNull(jira.Load());
        Assert.NotEqual(paths.CredentialsFile, paths.EverhourCredentialsFile);
    }

    /// <summary>
    /// Die alte asana.dat hält einen Asana-Token, den Hora nicht mehr braucht. Ein Geheimnis ohne
    /// Zweck gehört nicht auf die Platte.
    /// </summary>
    [Fact]
    public void LegacyAsanaConnectionIsDeleted()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Paths.LegacyAsanaCredentialsFile, new byte[] { 1, 2, 3 });

        DpapiEverhourCredentialStore.DeleteLegacyAsanaConnection(Paths);

        Assert.False(File.Exists(Paths.LegacyAsanaCredentialsFile));
    }

    [Fact]
    public void DeletingAMissingLegacyConnectionDoesNotThrow()
    {
        DpapiEverhourCredentialStore.DeleteLegacyAsanaConnection(Paths);
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
