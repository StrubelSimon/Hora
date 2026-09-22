namespace Hora.Core.Asana.Models;

/// <summary>Ein Zeiteintrag aus Asanas Zeiterfassung. Kennt nur einen Tag, keine Uhrzeit.</summary>
public sealed class AsanaTimeEntry
{
    public string Gid { get; set; } = string.Empty;

    public int DurationMinutes { get; set; }

    /// <summary>Der Tag, auf den der Eintrag gebucht ist. Asana liefert ihn als reines Datum.</summary>
    public DateOnly EnteredOn { get; set; }

    /// <summary>Das Projekt, dessen Budget der Eintrag belastet. Liefert den Text des Abzeichens.</summary>
    public AsanaProject? AttributableTo { get; set; }

    public AsanaUser? CreatedBy { get; set; }

    public List<AsanaTimeTrackingCategory> Categories { get; set; } = new();

    /// <summary>Siehe <see cref="AsanaTask"/>: möglicherweise nie gefüllt.</summary>
    public AsanaTask? Task { get; set; }
}

/// <summary>Frei vergebbare Beschriftung eines Zeiteintrags, z. B. „Entwicklung" oder „Meetings".</summary>
public sealed class AsanaTimeTrackingCategory
{
    public string Gid { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
