namespace Hora.Core.Asana.Models;

/// <summary>
/// Die Aufgabe hinter einem Zeiteintrag. Ob sie der Listenabruf überhaupt mitliefert, klärt die
/// Probe aus Task 1 des Umsetzungsplans — deshalb ist sie überall als fehlend zu behandeln.
/// </summary>
public sealed class AsanaTask
{
    public string Gid { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? PermalinkUrl { get; set; }
}
