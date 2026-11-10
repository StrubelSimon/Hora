using System.Globalization;
using System.Text.RegularExpressions;

namespace Hora.Core.Worklogs;

/// <summary>Ein Asana-Eintrag, der wegen einer Jira-Buchung desselben Tickets nicht mitzählt.</summary>
public sealed record HiddenAsanaEntry(WorklogEntry Entry, string TicketKey, DateOnly Date);

public sealed record JiraDuplicateResult(IReadOnlyList<WorklogEntry> Kept, IReadOnlyList<HiddenAsanaEntry> Hidden);

/// <summary>
/// Gleicht Asana-Einträge gegen die im selben Abruf geladenen Jira-Buchungen ab. In Asana gibt es
/// Spiegel-Aufgaben zu Jira-Tickets („SYN-2409 – Varda …“); wer dieselbe Arbeit auf beiden bucht,
/// würde sonst doppelt gezählt.
/// </summary>
/// <remarks>
/// Die Regel: Trägt der Asana-Taskname einen Ticketschlüssel und führt Jira dieses Ticket am selben
/// lokalen Tag — oder konnte Jira die Buchungen des Tickets nicht laden —, fällt der Asana-Eintrag
/// weg. Jira gewinnt, weil es für Tickets die Quelle ist. Das kann im Einzelfall zu wenig zeigen,
/// aber nie doppelt; der Warnsatz macht das Ausgeblendete sichtbar.
/// </remarks>
public static class JiraDuplicateFilter
{
    private const int MaxListedInWarning = 5;

    /// <summary>
    /// Jira-Schlüssel wie „SYN-2409“ oder „MAS-38“. Die Rahmen links und rechts verhindern, dass
    /// „SYN-24“ in „SYN-2409“ oder „SYN-2409“ in „XSYN-2409“ gefunden wird.
    /// </summary>
    private static readonly Regex TicketKeyPattern = new(
        @"(?<![A-Za-z0-9])[A-Z][A-Z0-9]+-\d+(?!\d)", RegexOptions.CultureInvariant);

    public static JiraDuplicateResult Apply(
        IReadOnlyList<WorklogEntry> jiraEntries,
        IReadOnlyList<string> failedIssueKeys,
        IReadOnlyList<WorklogEntry> asanaEntries,
        TimeZoneInfo zone)
    {
        var bookedInJira = jiraEntries
            .Select(entry => (entry.ItemKey, entry.LocalDate(zone)))
            .ToHashSet();
        var unloaded = failedIssueKeys.ToHashSet(StringComparer.Ordinal);

        var kept = new List<WorklogEntry>();
        var hidden = new List<HiddenAsanaEntry>();

        foreach (var entry in asanaEntries)
        {
            var date = entry.LocalDate(zone);
            var collision = TicketKeyPattern.Matches(entry.ItemSummary)
                .Select(match => match.Value)
                .FirstOrDefault(key => unloaded.Contains(key) || bookedInJira.Contains((key, date)));

            if (collision is null)
            {
                kept.Add(entry);
            }
            else
            {
                hidden.Add(new HiddenAsanaEntry(entry, collision, date));
            }
        }

        return new JiraDuplicateResult(kept, hidden);
    }

    /// <summary>Ein Satz für die Warnzeile der Übersicht; null, wenn nichts ausgeblendet wurde.</summary>
    public static string? FormatWarning(IReadOnlyList<HiddenAsanaEntry> hidden)
    {
        if (hidden.Count == 0)
        {
            return null;
        }

        var listed = hidden
            .OrderBy(item => item.Date)
            .ThenBy(item => item.TicketKey, StringComparer.Ordinal)
            .Take(MaxListedInWarning)
            .Select(item => $"{item.TicketKey} ({item.Date.ToString("dd.MM.", CultureInfo.InvariantCulture)})");

        var noun = hidden.Count == 1 ? "Everhour-Buchung" : "Everhour-Buchungen";
        var rest = hidden.Count > MaxListedInWarning ? $" und {hidden.Count - MaxListedInWarning} weitere" : string.Empty;

        return $"{hidden.Count} {noun} ausgeblendet, weil das Ticket am selben Tag in Jira geführt wird: " +
               $"{string.Join(", ", listed)}{rest}.";
    }
}
