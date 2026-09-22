namespace Hora.Core.Asana.Models;

/// <summary>Antwort von GET /users/me; auch als Urheber eines Zeiteintrags.</summary>
public sealed class AsanaUser
{
    public string Gid { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }

    /// <summary>Nur bei /users/me gefüllt und nur, wenn per opt_fields angefordert.</summary>
    public List<AsanaWorkspace> Workspaces { get; set; } = new();
}
