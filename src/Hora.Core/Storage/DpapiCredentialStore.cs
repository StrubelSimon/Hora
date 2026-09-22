using System.Security.Cryptography;
using System.Text.Json;

namespace Hora.Core.Storage;

/// <summary>
/// Speichert Zugangsdaten mit Windows DPAPI (CurrentUser). Nur derselbe Windows-Benutzer
/// auf demselben Rechner kann die Datei wieder entschlüsseln.
/// </summary>
public sealed class DpapiCredentialStore : ICredentialStore
{
    private readonly AppPaths _paths;

    public DpapiCredentialStore(AppPaths paths)
    {
        _paths = paths;
    }

    public void Save(StoredCredentials credentials)
    {
        _paths.EnsureRootExists();
        var plainBytes = JsonSerializer.SerializeToUtf8Bytes(credentials);
        var protectedBytes = ProtectedData.Protect(plainBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_paths.CredentialsFile, protectedBytes);
    }

    public StoredCredentials? Load()
    {
        if (!File.Exists(_paths.CredentialsFile))
        {
            return null;
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(_paths.CredentialsFile);
            var plainBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            var credentials = JsonSerializer.Deserialize<StoredCredentials>(plainBytes);

            if (credentials is null || string.IsNullOrEmpty(credentials.ApiToken))
            {
                Delete();
                return null;
            }

            return credentials;
        }
        catch (Exception)
        {
            // Unlesbar bedeutet "nicht vorhanden": Datei beschädigt, von einem anderen Benutzer oder
            // wegen fehlender Zugriffsrechte nicht lesbar. Der Filter nannte zuvor nur
            // CryptographicException, JsonException und IOException; eine UnauthorizedAccessException
            // entkam und riss den Programmstart mit (Befund F).
            Delete();
            return null;
        }
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(_paths.CredentialsFile))
            {
                File.Delete(_paths.CredentialsFile);
            }
        }
        catch (Exception)
        {
            // Löschen darf nicht werfen: Load() ruft es im eigenen Fehlerzweig auf — eine Ausnahme
            // von hier würde den gerade abgefangenen Fehler durch einen neuen ersetzen. Beim Abmelden
            // gilt dasselbe: Der Nutzer käme sonst nicht zurück auf den Anmeldebildschirm. Bleibt die
            // Datei liegen, ist sie weiterhin DPAPI-verschlüsselt, und der nächste Start prüft sie
            // ohnehin erneut bei Jira.
        }
    }
}
