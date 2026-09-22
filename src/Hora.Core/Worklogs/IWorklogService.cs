namespace Hora.Core.Worklogs;

public interface IWorklogService
{
    /// <summary>Eigene Buchungen aller Tage von <paramref name="from"/> bis <paramref name="to"/> (beide inklusive, lokale Zeitzone).</summary>
    Task<WorklogFetchResult> GetEntriesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
