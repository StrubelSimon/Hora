namespace Hora.Core.Asana.Models;

/// <summary>Eine Organisation oder ein Arbeitsbereich. Entspricht dem, was der Nutzer „Server" nennt.</summary>
public sealed class AsanaWorkspace
{
    public string Gid { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
