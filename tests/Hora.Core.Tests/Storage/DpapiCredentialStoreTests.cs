using System.Text;
using Hora.Core.Storage;

namespace Hora.Core.Tests.Storage;

public sealed class DpapiCredentialStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HoraTests", Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;
    private readonly DpapiCredentialStore _store;

    public DpapiCredentialStoreTests()
    {
        _paths = new AppPaths(_root);
        _store = new DpapiCredentialStore(_paths);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Save_then_Load_roundtrips_credentials()
    {
        var credentials = new StoredCredentials("https://example.atlassian.net", "a@b.de", "geheimes-token");

        _store.Save(credentials);
        var loaded = _store.Load();

        Assert.Equal(credentials, loaded);
    }

    [Fact]
    public void Save_does_not_write_token_or_email_in_plain_text()
    {
        _store.Save(new StoredCredentials("https://example.atlassian.net", "a@b.de", "geheimes-token"));

        var content = Encoding.UTF8.GetString(File.ReadAllBytes(_paths.CredentialsFile));

        Assert.DoesNotContain("geheimes-token", content);
        Assert.DoesNotContain("a@b.de", content);
    }

    [Fact]
    public void Load_returns_null_when_no_file_exists()
    {
        Assert.Null(_store.Load());
    }

    [Fact]
    public void Load_returns_null_and_deletes_corrupt_file()
    {
        _paths.EnsureRootExists();
        File.WriteAllBytes(_paths.CredentialsFile, new byte[] { 1, 2, 3, 4, 5 });

        var loaded = _store.Load();

        Assert.Null(loaded);
        Assert.False(File.Exists(_paths.CredentialsFile));
    }

    [Fact]
    public void Load_returns_null_when_the_file_cannot_be_read()
    {
        // Befund F: Der Filter nannte nur CryptographicException, JsonException und IOException.
        // Eine UnauthorizedAccessException entkam und riss den Programmstart mit.
        _store.Save(new StoredCredentials("https://example.atlassian.net", "a@b.de", "geheimes-token"));

        using var denial = FileReadDenial.For(_paths.CredentialsFile);
        // Vorbedingung: Das Leserecht ist wirklich entzogen.
        Assert.Throws<UnauthorizedAccessException>(() => File.ReadAllBytes(_paths.CredentialsFile));

        Assert.Null(_store.Load());
    }

    [Fact]
    public void Delete_does_not_throw_when_the_file_cannot_be_deleted()
    {
        // Befund F: Delete() wird auch aus dem Fehlerzweig von Load() heraus aufgerufen und beim
        // Abmelden. Wirft es dort, ersetzt es den gerade abgefangenen Fehler durch einen neuen
        // beziehungsweise verhindert die Rückkehr zum Anmeldebildschirm.
        _store.Save(new StoredCredentials("https://example.atlassian.net", "a@b.de", "t"));
        using var exclusiveHandle = new FileStream(
            _paths.CredentialsFile, FileMode.Open, FileAccess.Read, FileShare.None);

        _store.Delete(); // darf nicht werfen

        Assert.True(File.Exists(_paths.CredentialsFile));
    }

    [Fact]
    public void Delete_removes_file_and_tolerates_missing_file()
    {
        _store.Save(new StoredCredentials("https://example.atlassian.net", "a@b.de", "t"));

        _store.Delete();
        Assert.False(File.Exists(_paths.CredentialsFile));

        _store.Delete(); // zweiter Aufruf darf nicht werfen
    }
}
