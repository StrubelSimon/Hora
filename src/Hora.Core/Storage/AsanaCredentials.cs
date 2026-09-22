namespace Hora.Core.Storage;

/// <summary>
/// Die Asana-Verbindung, verschlüsselt auf der Platte. Token und gewählter Workspace liegen
/// zusammen in einem Satz, damit „verbunden" ein einziges, atomares Ding bleibt.
/// </summary>
public sealed record AsanaCredentials(
    string Token,
    string UserGid,
    string UserName,
    string WorkspaceGid,
    string WorkspaceName);
