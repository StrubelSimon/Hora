using Hora.Core.Everhour.Models;

namespace Hora.Core.Everhour;

/// <summary>Lesender Zugriff auf die Everhour-API. Hora schreibt nie nach Everhour.</summary>
public interface IEverhourClient
{
    /// <summary>GET /users/me – prüft den Schlüssel und liefert Kennung und Namen seines Inhabers.</summary>
    Task<EverhourUser> GetMyselfAsync(CancellationToken cancellationToken);

    /// <summary>Alle Zeiteinträge eines Nutzers zwischen zwei Tagen (beide inklusive), entdoppelt nach Kennung.</summary>
    Task<IReadOnlyList<EverhourTimeEntry>> GetTimeEntriesAsync(
        long userId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    /// <summary>GET /projects/{id} – für den Projektnamen, den der Zeiteintrag nicht mitliefert.</summary>
    Task<EverhourProject> GetProjectAsync(string projectId, CancellationToken cancellationToken);
}
