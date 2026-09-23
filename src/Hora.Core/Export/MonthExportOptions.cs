namespace Hora.Core.Export;

/// <summary>
/// Die beiden Schalter des Export-Abschnitts. Als Parameter hereingereicht, nicht als Zustand des
/// Bauers gehalten — dieselbe Entscheidung wie beim DurationFormat.
/// </summary>
/// <param name="IncludeIssueRows">Tickets unter jedem Tag aufschlüsseln.</param>
/// <param name="IncludeTargets">Die Spalten „Soll“ und „Diff“ mitschreiben.</param>
public sealed record MonthExportOptions(bool IncludeIssueRows = true, bool IncludeTargets = true)
{
    public static MonthExportOptions Default { get; } = new();
}
