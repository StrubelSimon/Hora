namespace Hora.Core.Storage;

public interface IAsanaCredentialStore
{
    void Save(AsanaCredentials credentials);

    /// <summary>Liefert null, wenn keine Verbindung besteht oder die Datei nicht lesbar ist.</summary>
    AsanaCredentials? Load();

    void Delete();
}
