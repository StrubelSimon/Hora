namespace Hora.Core.Storage;

/// <summary>
/// Speichert Zugangsdaten mit Windows DPAPI (CurrentUser). Nur derselbe Windows-Benutzer
/// auf demselben Rechner kann die Datei wieder entschlüsseln.
/// </summary>
public sealed class DpapiCredentialStore : ICredentialStore
{
    private readonly DpapiBlobStore<StoredCredentials> _blobs;

    public DpapiCredentialStore(AppPaths paths)
    {
        _blobs = new DpapiBlobStore<StoredCredentials>(paths.CredentialsFile);
    }

    public void Save(StoredCredentials credentials) => _blobs.Save(credentials);

    public StoredCredentials? Load()
    {
        var credentials = _blobs.Load();

        if (credentials is null || string.IsNullOrEmpty(credentials.ApiToken))
        {
            Delete();
            return null;
        }

        return credentials;
    }

    public void Delete() => _blobs.Delete();
}
