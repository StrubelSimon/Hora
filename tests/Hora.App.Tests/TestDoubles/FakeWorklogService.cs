using Hora.Core.Worklogs;

namespace Hora.App.Tests.TestDoubles;

internal sealed class FakeWorklogService : IWorklogService
{
    public List<(DateOnly From, DateOnly To)> Calls { get; } = new();

    public Func<DateOnly, DateOnly, WorklogFetchResult> Respond { get; set; } =
        (_, _) => new WorklogFetchResult(Array.Empty<WorklogEntry>(), Array.Empty<string>(), Array.Empty<string>());

    /// <summary>Wird beim nächsten Aufruf geworfen und dann zurückgesetzt.</summary>
    public Exception? ThrowOnce { get; set; }

    /// <summary>
    /// Wenn gesetzt, wartet der nächste Aufruf auf dieses Signal statt nur auf <see cref="Task.Yield"/>.
    /// Damit lässt sich der Abschluss eines Abrufs von außen steuern, etwa um zwei überlappende
    /// Abrufe gezielt zu erzeugen. Wird pro Aufruf ausgelesen (Standard: null, unverändertes
    /// Verhalten) — für einen weiteren gesteuerten Aufruf muss es erneut gesetzt werden.
    /// </summary>
    public TaskCompletionSource? Gate { get; set; }

    public async Task<WorklogFetchResult> GetEntriesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        Calls.Add((from, to));

        if (Gate is { } gate)
        {
            await gate.Task;
        }
        else
        {
            await Task.Yield();
        }

        if (ThrowOnce is { } failure)
        {
            ThrowOnce = null;
            throw failure;
        }

        return Respond(from, to);
    }
}
