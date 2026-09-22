namespace Hora.Core.Worklogs;

/// <summary>
/// Darstellung einer Dauer. Wird überall als Parameter hereingereicht, nie als statischer
/// veränderlicher Zustand gehalten: Ein solcher Zustand machte die Tests voneinander abhängig.
/// </summary>
public enum DurationFormat
{
    /// <summary>Bisherige Schreibweise im Jira-Stil, zum Beispiel „7h 45m“.</summary>
    HoursMinutes,

    /// <summary>Dezimalstunden mit Komma, zum Beispiel „7,75h“.</summary>
    DecimalHours,
}
