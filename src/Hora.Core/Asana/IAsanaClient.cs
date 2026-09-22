using Hora.Core.Asana.Models;

namespace Hora.Core.Asana;

public interface IAsanaClient
{
    /// <summary>GET /users/me – prüft den Token und liefert Kennung, Name, E-Mail und die Workspaces.</summary>
    Task<AsanaUser> GetMyselfAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Alle Zeiteinträge eines Nutzers in einem Workspace zwischen zwei Tagen (beide inklusive).
    /// Ein Aufruf je Zeitraum, seitenweise nachgeladen.
    /// </summary>
    Task<IReadOnlyList<AsanaTimeEntry>> GetTimeEntriesAsync(
        string userGid,
        string workspaceGid,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}
