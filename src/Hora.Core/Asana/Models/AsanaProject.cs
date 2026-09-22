namespace Hora.Core.Asana.Models;

/// <summary>Das Projekt, dem ein Zeiteintrag zugerechnet wird (`attributable_to`).</summary>
public sealed class AsanaProject
{
    public string Gid { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
