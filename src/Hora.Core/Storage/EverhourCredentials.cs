namespace Hora.Core.Storage;

/// <summary>
/// Die Everhour-Verbindung, verschlüsselt auf der Platte. Schlüssel und geprüfte Nutzerkennung
/// liegen zusammen in einem Satz, damit „verbunden“ ein einziges, atomares Ding bleibt.
/// </summary>
public sealed record EverhourCredentials(string ApiKey, long UserId, string UserName);

public interface IEverhourCredentialStore
{
    void Save(EverhourCredentials credentials);

    /// <summary>Liefert null, wenn keine Verbindung besteht oder die Datei nicht lesbar ist.</summary>
    EverhourCredentials? Load();

    void Delete();
}
