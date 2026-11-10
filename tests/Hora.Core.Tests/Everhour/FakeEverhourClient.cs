using Hora.Core.Everhour;
using Hora.Core.Everhour.Models;

namespace Hora.Core.Tests.Everhour;

/// <summary>Liefert vorbereitete Zeiteinträge und Projekte und merkt sich, was angefragt wurde.</summary>
internal sealed class FakeEverhourClient : IEverhourClient
{
    private readonly List<EverhourTimeEntry> _entries;
    private readonly Exception? _throwOnEntries;

    public FakeEverhourClient(IEnumerable<EverhourTimeEntry>? entries = null, Exception? throwOnEntries = null)
    {
        _entries = entries?.ToList() ?? new List<EverhourTimeEntry>();
        _throwOnEntries = throwOnEntries;
    }

    public EverhourUser Myself { get; set; } = new() { Id = 1431001, Name = "Simon Strubel" };

    /// <summary>Projektname je Kennung; fehlt eine Kennung, wirft der Abruf <see cref="ProjectFailure"/>.</summary>
    public Dictionary<string, string> ProjectNames { get; } = new();

    public Exception ProjectFailure { get; set; } = new EverhourUnavailableException("Projekt weg", 404);

    public List<string> RequestedProjects { get; } = new();

    public long? LastUserId { get; private set; }

    public DateOnly? LastFrom { get; private set; }

    public DateOnly? LastTo { get; private set; }

    public Task<EverhourUser> GetMyselfAsync(CancellationToken cancellationToken) => Task.FromResult(Myself);

    public Task<IReadOnlyList<EverhourTimeEntry>> GetTimeEntriesAsync(
        long userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        LastUserId = userId;
        LastFrom = from;
        LastTo = to;

        if (_throwOnEntries is not null)
        {
            throw _throwOnEntries;
        }

        return Task.FromResult<IReadOnlyList<EverhourTimeEntry>>(_entries);
    }

    public Task<EverhourProject> GetProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        RequestedProjects.Add(projectId);
        return ProjectNames.TryGetValue(projectId, out var name)
            ? Task.FromResult(new EverhourProject { Id = projectId, Name = name })
            : throw ProjectFailure;
    }
}
