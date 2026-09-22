using Hora.Core.Asana;
using Hora.Core.Asana.Models;

namespace Hora.Core.Tests.Asana;

/// <summary>Liefert vorbereitete Zeiteinträge und merkt sich den zuletzt angefragten Zeitraum.</summary>
internal sealed class FakeAsanaClient : IAsanaClient
{
    private readonly List<AsanaTimeEntry> _entries;
    private readonly Exception? _throwOnEntries;

    public FakeAsanaClient(IEnumerable<AsanaTimeEntry>? entries = null, Exception? throwOnEntries = null)
    {
        _entries = entries?.ToList() ?? new List<AsanaTimeEntry>();
        _throwOnEntries = throwOnEntries;
    }

    public AsanaUser Myself { get; set; } = new() { Gid = "111", Name = "Simon Strubel" };

    public DateOnly? LastFrom { get; private set; }

    public DateOnly? LastTo { get; private set; }

    public string? LastUserGid { get; private set; }

    public string? LastWorkspaceGid { get; private set; }

    public Task<AsanaUser> GetMyselfAsync(CancellationToken cancellationToken) => Task.FromResult(Myself);

    public Task<IReadOnlyList<AsanaTimeEntry>> GetTimeEntriesAsync(
        string userGid, string workspaceGid, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        LastUserGid = userGid;
        LastWorkspaceGid = workspaceGid;
        LastFrom = from;
        LastTo = to;

        if (_throwOnEntries is not null)
        {
            throw _throwOnEntries;
        }

        return Task.FromResult<IReadOnlyList<AsanaTimeEntry>>(_entries);
    }
}
