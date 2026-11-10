namespace Hora.Core.Everhour.Models;

/// <summary>Der Inhaber des API-Schlüssels, aus GET /users/me.</summary>
public sealed class EverhourUser
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }
}

/// <summary>
/// Ein Zeiteintrag. Everhour hält höchstens einen Eintrag je Nutzer, Tag und Aufgabe und kennt
/// nur den Tag, keine Uhrzeit.
/// </summary>
public sealed class EverhourTimeEntry
{
    public long Id { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>Dauer in Sekunden.</summary>
    public int Time { get; set; }

    public string? Comment { get; set; }

    /// <summary>Fehlt bei Einträgen ohne Aufgabe — die sind fast immer leere Reste mit 0 Sekunden.</summary>
    public EverhourTask? Task { get; set; }
}

/// <summary>
/// Die Aufgabe hinter einem Zeiteintrag. Die Kennung trägt die Herkunft als Präfix:
/// <c>as:</c> für Asana, <c>jr:</c> für Jira.
/// </summary>
public sealed class EverhourTask
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Url { get; set; }

    /// <summary>Nur Kennungen; den Namen liefert erst GET /projects/{id}.</summary>
    public List<string> Projects { get; set; } = new();
}

public sealed class EverhourProject
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
