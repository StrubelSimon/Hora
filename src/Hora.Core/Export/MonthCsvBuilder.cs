using System.Globalization;
using Hora.Core.Worklogs;

namespace Hora.Core.Export;

/// <summary>
/// Baut den Inhalt einer Export-Datei: Kopfblock, eine Zeile je gebuchtem Tag, darunter eingerückt
/// eine Zeile je Ticket, zum Schluss die Summe. Reine Rechnerei ohne Ein- und Ausgabe — weder Uhr
/// noch Datei noch Jira; alles kommt als Parameter herein. Das Dateiformat hängt damit
/// vollständig an den Modultests.
/// </summary>
public static class MonthCsvBuilder
{
    /// <summary>
    /// Feste Kultur, kein CultureInfo.CurrentCulture: Das Komma ist eine Vorgabe des Entwurfs,
    /// genau wie im DurationFormatter, und darf nicht an der Windows-Ländereinstellung hängen.
    /// </summary>
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    private const string DateFormat = "dd.MM.yyyy";

    private const string TimestampFormat = "dd.MM.yyyy HH:mm";

    /// <summary>Die zwei Leerzeichen, die die Ticketzeilen unter ihren Tag rücken.</summary>
    private const string IssueIndent = "  ";

    /// <param name="month">Irgendein Tag des gewünschten Monats; verwendet werden nur Jahr und Monat.</param>
    /// <param name="generatedAt">Zeitpunkt für den Kopfblock. Hereingereicht, damit der Bauer ohne Uhr auskommt.</param>
    public static MonthExportResult Build(
        IReadOnlyList<WorklogEntry> entries,
        DateOnly month,
        WorkSchedule schedule,
        TimeZoneInfo zone,
        MonthExportOptions options,
        string userName,
        DateTimeOffset generatedAt)
    {
        var firstDay = new DateOnly(month.Year, month.Month, 1);
        var dayCountOfMonth = DateTime.DaysInMonth(month.Year, month.Month);
        var allDaysOfMonth = Enumerable.Range(0, dayCountOfMonth).Select(firstDay.AddDays).ToList();

        // Der Abrufdienst liefert genau diesen Monat; gefiltert wird trotzdem. Eine Buchung um
        // 0:15 Uhr des Folgemonats gehört nach lokaler Zeit nicht mehr hierher, und der Bauer soll
        // sich nicht darauf verlassen, dass jemand anders das vorher abgeschnitten hat.
        var daysWithBookings = entries
            .Select(entry => (Entry: entry, Date: entry.LocalDate(zone)))
            .Where(pair => pair.Date.Year == month.Year && pair.Date.Month == month.Month)
            .GroupBy(pair => pair.Date)
            .OrderBy(group => group.Key)
            .Select(group => (Date: group.Key, Entries: group.Select(pair => pair.Entry).ToList()))
            .ToList();

        var monthTarget = allDaysOfMonth.Aggregate(
            TimeSpan.Zero,
            (sum, day) => sum + schedule.HoursFor(day.DayOfWeek));
        var workdayCount = allDaysOfMonth.Count(day => schedule.HoursFor(day.DayOfWeek) > TimeSpan.Zero);
        var monthTotal = daysWithBookings.Aggregate(TimeSpan.Zero, (sum, day) => sum + Sum(day.Entries));

        var writer = new CsvWriter();

        writer.WriteRow("Hora – Zeitexport");
        writer.WriteRow("Mitarbeiter", userName);
        writer.WriteRow("Zeitraum", WeekCalendar.MonthLabel(firstDay));
        writer.WriteRow("Erstellt am", generatedAt.ToString(TimestampFormat, CultureInfo.InvariantCulture));
        writer.WriteRow("Arbeitstage im Monat", workdayCount.ToString(CultureInfo.InvariantCulture));
        writer.WriteRow("Tage mit Buchungen", daysWithBookings.Count.ToString(CultureInfo.InvariantCulture));
        writer.WriteBlankRow();

        WriteRowOfCells(writer, options, "Datum", "Tag", "Ticket", "Beschreibung", "Stunden", "Soll", "Diff");

        foreach (var (date, dayEntries) in daysWithBookings)
        {
            var total = Sum(dayEntries);
            var target = schedule.HoursFor(date.DayOfWeek);

            WriteRowOfCells(
                writer,
                options,
                date.ToString(DateFormat, CultureInfo.InvariantCulture),
                WeekCalendar.ShortDayName(date),
                null,
                null,
                Hours(total),
                Hours(target),
                Hours(total - target));

            if (!options.IncludeIssueRows)
            {
                continue;
            }

            // Dieselbe Ordnung wie in der Übersicht: die längste Buchung zuerst.
            foreach (var issue in WorklogAggregator.BuildIssueTotals(dayEntries))
            {
                WriteRowOfCells(
                    writer,
                    options,
                    null,
                    null,
                    IssueIndent + issue.ItemKey,
                    issue.ItemSummary,
                    Hours(issue.Total),
                    null,
                    null);
            }
        }

        WriteRowOfCells(
            writer,
            options,
            "Summe",
            null,
            null,
            null,
            Hours(monthTotal),
            Hours(monthTarget),
            Hours(monthTotal - monthTarget));

        return new MonthExportResult(writer.Build(), DecimalHours(monthTotal), daysWithBookings.Count);
    }

    /// <summary>
    /// Schreibt eine Zeile und lässt die beiden letzten Spalten weg, wenn die Sollwerte abgewählt
    /// sind. An einer Stelle entschieden statt an sieben Aufrufstellen: Sonst wäre die Kopfzeile
    /// irgendwann siebenspaltig und die Summenzeile fünfspaltig.
    /// </summary>
    private static void WriteRowOfCells(
        CsvWriter writer,
        MonthExportOptions options,
        string? date,
        string? weekday,
        string? issueKey,
        string? summary,
        string? hours,
        string? target,
        string? difference)
    {
        if (options.IncludeTargets)
        {
            writer.WriteRow(date, weekday, issueKey, summary, hours, target, difference);
        }
        else
        {
            writer.WriteRow(date, weekday, issueKey, summary, hours);
        }
    }

    private static TimeSpan Sum(IEnumerable<WorklogEntry> entries) =>
        entries.Aggregate(TimeSpan.Zero, (sum, entry) => sum + entry.Duration);

    /// <summary>
    /// Dezimalstunden mit Komma und zwei Nachkommastellen. Der Export folgt bewusst nicht dem
    /// Zeitformat des Profils: „7h 45m“ wäre für Excel Text, mit „7,75“ lässt sich rechnen.
    /// </summary>
    private static string Hours(TimeSpan duration) => DecimalHours(duration).ToString("0.00", German);

    /// <summary>
    /// Gerundet wird erst hier, am fertigen Wert. Die Tagessumme entsteht vorher aus ungerundeten
    /// TimeSpan-Werten — sonst ergäben drei Buchungen zu zwanzig Minuten in der Spalte 0,99 statt
    /// einer glatten Stunde.
    /// </summary>
    private static decimal DecimalHours(TimeSpan duration) =>
        Math.Round((decimal)duration.TotalHours, 2, MidpointRounding.AwayFromZero);
}
