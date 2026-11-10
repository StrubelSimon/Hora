namespace Hora.Core.Storage;

/// <summary>Speichert die Everhour-Verbindung in everhour.dat, verschlüsselt mit Windows DPAPI.</summary>
public sealed class DpapiEverhourCredentialStore : IEverhourCredentialStore
{
    private readonly DpapiBlobStore<EverhourCredentials> _blobs;

    public DpapiEverhourCredentialStore(AppPaths paths)
    {
        _blobs = new DpapiBlobStore<EverhourCredentials>(paths.EverhourCredentialsFile);
    }

    /// <summary>
    /// Löscht die frühere Asana-Verbindung. Sie hält einen Asana-Token, den Hora seit dem Wechsel
    /// auf Everhour nicht mehr liest — ein Geheimnis ohne Zweck gehört nicht auf die Platte.
    /// </summary>
    public static void DeleteLegacyAsanaConnection(AppPaths paths)
    {
        if (File.Exists(paths.LegacyAsanaCredentialsFile))
        {
            File.Delete(paths.LegacyAsanaCredentialsFile);
        }
    }

    public void Save(EverhourCredentials credentials) => _blobs.Save(credentials);

    public EverhourCredentials? Load()
    {
        var credentials = _blobs.Load();

        // Ein leerer Schlüssel ist so wertlos wie gar keiner: Er täuschte eine Verbindung vor, die
        // bei jedem Abruf mit HTTP 401 endete. Dieselbe Vorsorge trifft DpapiCredentialStore.
        if (credentials is null || string.IsNullOrEmpty(credentials.ApiKey))
        {
            Delete();
            return null;
        }

        return credentials;
    }

    public void Delete() => _blobs.Delete();
}
