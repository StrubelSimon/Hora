using Hora.Core.Asana;
using Hora.Core.Logging;

namespace Hora.Core.Worklogs;

/// <summary>
/// Legt die Buchungen mehrerer Quellen zu einer Liste zusammen. Jira ist gesetzt, Asana ein
/// Steckplatz, der zur Laufzeit besetzt und wieder geleert wird — so wirken Verbinden und Trennen
/// auf der Profilseite, ohne die Übersicht neu aufzubauen.
/// </summary>
/// <remarks>
/// Der tragende Grundsatz: Ein Asana-Problem darf die Jira-Ansicht nie leeren. Jeder Asana-Fehler
/// wird deshalb zu einem Satz in <see cref="WorklogFetchResult.Warnings"/>, während die
/// Jira-Einträge unverändert durchgereicht werden. Jira-Fehler laufen dagegen weiter nach oben:
/// An ihnen unterscheidet die Hülle abgelaufene Anmeldung und Störung.
/// </remarks>
public sealed class CompositeWorklogService : IWorklogService
{
    private readonly IWorklogService _jira;
    private readonly IAppLogger _logger;

    // Wird aus dem Oberflächenfaden gesetzt und im Abruf gelesen. Ein einzelnes Feld, dessen
    // Schreibvorgang unteilbar ist; ein Schloss wäre hier Aufwand ohne Gewinn.
    private volatile IWorklogService? _asana;

    public CompositeWorklogService(IWorklogService jira, IAppLogger logger)
    {
        _jira = jira;
        _logger = logger;
    }

    /// <summary>Asana hat den Token abgelehnt; die gespeicherte Verbindung ist wertlos geworden.</summary>
    public event Action? AsanaTokenRejected;

    /// <summary>Besetzt den Asana-Steckplatz oder leert ihn mit <c>null</c>.</summary>
    public void SetAsanaProvider(IWorklogService? asana) => _asana = asana;

    public async Task<WorklogFetchResult> GetEntriesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var jiraResult = await _jira.GetEntriesAsync(from, to, cancellationToken);

        var asana = _asana;
        if (asana is null)
        {
            return jiraResult;
        }

        IReadOnlyList<WorklogEntry> asanaEntries = Array.Empty<WorklogEntry>();
        var warnings = new List<string>(jiraResult.Warnings);

        try
        {
            asanaEntries = (await asana.GetEntriesAsync(from, to, cancellationToken)).Entries;
        }
        catch (OperationCanceledException)
        {
            throw; // Nutzer hat weiternavigiert; das ist kein Asana-Fehler.
        }
        catch (AsanaAuthenticationException ex)
        {
            _logger.Error("Asana hat den Token abgelehnt", ex);
            AsanaTokenRejected?.Invoke();
            warnings.Add("Asana-Token ist ungültig. Bitte neu verbinden.");
        }
        catch (AsanaFeatureUnavailableException ex)
        {
            _logger.Error("Asana-Tarif ohne Zeiterfassung", ex);
            warnings.Add("Dieser Asana-Tarif enthält keine Zeiterfassung.");
        }
        catch (AsanaRateLimitException ex)
        {
            _logger.Error("Asana begrenzt gerade die Anfragen", ex);
            warnings.Add("Asana begrenzt gerade die Anfragen. Die Asana-Zeiten fehlen in dieser Ansicht.");
        }
        catch (AsanaException ex)
        {
            _logger.Error("Asana-Zeiten konnten nicht geladen werden", ex);
            warnings.Add("Asana ist nicht erreichbar. Die Asana-Zeiten fehlen in dieser Ansicht.");
        }
        catch (Exception ex)
        {
            // Auffangzweig als letzter, hinter den durchreichenden: Eine unerwartete Ausnahme aus
            // der Asana-Hälfte darf den Abruf nicht beenden und die Jira-Zahlen mitnehmen.
            _logger.Error("Asana-Zeiten konnten unerwartet nicht geladen werden", ex);
            warnings.Add("Asana-Zeiten konnten nicht geladen werden. Einzelheiten stehen im Protokoll.");
        }

        if (asanaEntries.Count == 0)
        {
            return jiraResult with { Warnings = warnings };
        }

        // Zweiter und dritter Sortierschlüssel: Alle Asana-Einträge eines Tags tragen denselben
        // Zeitstempel (Mitternacht). Ohne sie tauschten sie bei jedem Aktualisieren die Plätze —
        // derselbe Fehler, der bei Jira als Befund G behoben wurde.
        var merged = jiraResult.Entries
            .Concat(asanaEntries)
            .OrderBy(entry => entry.Started)
            .ThenBy(entry => entry.Source)
            .ThenBy(entry => entry.ItemKey, StringComparer.Ordinal)
            .ToList();

        return new WorklogFetchResult(merged, jiraResult.FailedIssueKeys, warnings);
    }
}
