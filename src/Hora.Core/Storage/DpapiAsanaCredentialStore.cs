namespace Hora.Core.Storage;

/// <summary>Speichert die Asana-Verbindung in asana.dat, verschlüsselt mit Windows DPAPI.</summary>
public sealed class DpapiAsanaCredentialStore : IAsanaCredentialStore
{
    private readonly DpapiBlobStore<AsanaCredentials> _blobs;

    public DpapiAsanaCredentialStore(AppPaths paths)
    {
        _blobs = new DpapiBlobStore<AsanaCredentials>(paths.AsanaCredentialsFile);
    }

    public void Save(AsanaCredentials credentials) => _blobs.Save(credentials);

    public AsanaCredentials? Load()
    {
        var credentials = _blobs.Load();

        // Ein leerer Token ist so wertlos wie gar keiner: Er täuschte eine Verbindung vor, die bei
        // jedem Abruf mit HTTP 401 endete. Dieselbe Vorsorge trifft DpapiCredentialStore.
        if (credentials is null || string.IsNullOrEmpty(credentials.Token))
        {
            Delete();
            return null;
        }

        return credentials;
    }

    public void Delete() => _blobs.Delete();
}
