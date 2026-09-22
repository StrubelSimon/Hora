namespace Hora.Core.Storage;

public interface ICredentialStore
{
    void Save(StoredCredentials credentials);

    /// <summary>Liefert null, wenn keine Datei existiert oder sie nicht entschlüsselt werden kann.</summary>
    StoredCredentials? Load();

    void Delete();
}
