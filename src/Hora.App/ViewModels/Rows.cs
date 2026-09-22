using System.Globalization;
using Hora.App.Services;
using Hora.Core.Worklogs;

namespace Hora.App.ViewModels;

/// <summary>Eine Zeile der Tagesansicht mit fertig formatierten Texten.</summary>
public sealed record WorklogRow(
    string IssueKey,
    string IssueSummary,
    string Comment,
    string StartText,
    string DurationText,
    string IssueUrl,
    string TimeRangeText,
    bool HasComment)
{
    public static WorklogRow From(
        WorklogEntry entry,
        TimeZoneInfo zone,
        string baseUrl,
        DurationFormat format = DurationFormat.HoursMinutes)
    {
        var localStart = TimeZoneInfo.ConvertTime(entry.Started, zone);
        // Die Endzeit wird aus dem Zeitpunkt (Start + Dauer) in die Zielzone umgerechnet, nicht im
        // Offset des Startzeitpunkts addiert: An einem Zeitumstellungstag wechselt der Offset
        // dazwischen, und eine Buchung 01:30 plus zwei Stunden stünde sonst als "01:30 – 03:30"
        // statt "01:30 – 04:30" da (Befund H). Die Summen waren davon nie betroffen.
        var localEnd = TimeZoneInfo.ConvertTime(entry.Started + entry.Duration, zone);
        var startText = localStart.ToString("HH:mm", CultureInfo.InvariantCulture);
        var endText = localEnd.ToString("HH:mm", CultureInfo.InvariantCulture);

        return new(
            entry.IssueKey,
            entry.IssueSummary,
            entry.Comment,
            startText,
            DurationFormatter.Format(entry.Duration, format),
            IssueUrlFor(baseUrl, entry.IssueKey),
            $"{startText} – {endText}",
            !string.IsNullOrEmpty(entry.Comment));
    }

    public static string IssueUrlFor(string baseUrl, string issueKey) => $"{baseUrl.TrimEnd('/')}/browse/{issueKey}";
}

/// <summary>Eine Zeile im Abschnitt „Pro Ticket“.</summary>
public sealed record IssueTotalRow(
    string IssueKey,
    string IssueSummary,
    string TotalText,
    int EntryCount,
    string IssueUrl,
    double BarRatio)
{
    /// <summary>
    /// <paramref name="maxTotal"/> ist die größte Gesamtdauer der angezeigten Menge; daran bemisst sich
    /// der Balken (Abschnitt 3.7 der Gestaltungsvorgaben). Ist sie null – leere Menge oder lauter
    /// Einträge ohne Dauer –, ist die Verhältniszahl null, statt durch null zu teilen.
    /// </summary>
    public static IssueTotalRow From(
        IssueTotal total,
        string baseUrl,
        TimeSpan maxTotal,
        DurationFormat format = DurationFormat.HoursMinutes) => new(
        total.IssueKey,
        total.IssueSummary,
        DurationFormatter.Format(total.Total, format),
        total.EntryCount,
        WorklogRow.IssueUrlFor(baseUrl, total.IssueKey),
        BarRatioFor(total.Total, maxTotal));

    /// <summary>Größte Gesamtdauer einer Menge von Ticketsummen; leere Menge ergibt null.</summary>
    public static TimeSpan MaxTotalOf(IReadOnlyList<IssueTotal> totals) =>
        totals.Count == 0 ? TimeSpan.Zero : totals.Max(total => total.Total);

    private static double BarRatioFor(TimeSpan total, TimeSpan maxTotal) =>
        maxTotal > TimeSpan.Zero
            ? Math.Clamp(total.TotalHours / maxTotal.TotalHours, 0.0, 1.0)
            : 0.0;
}

/// <summary>Eine Tageszeile der Wochenansicht.</summary>
public sealed record WeekDayRow(
    DateOnly Date,
    string Label,
    int EntryCount,
    string TotalText,
    bool IsToday,
    string DayName,
    string DateText,
    double BarRatio)
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>
    /// <paramref name="dailyTarget"/> ist die Sollzeit dieses einen Wochentags; <paramref name="weekMaxTotal"/>
    /// die größte Tagessumme der ganzen Woche. Ist das Tagessoll null (typischerweise am Wochenende),
    /// bemisst sich der Balken stattdessen am Wochenmaximum (Abschnitt 3.5 der Gestaltungsvorgaben).
    /// </summary>
    public static WeekDayRow From(
        DaySummary day,
        DateOnly today,
        TimeSpan dailyTarget,
        TimeSpan weekMaxTotal,
        DurationFormat format = DurationFormat.HoursMinutes) => new(
        day.Date,
        WeekCalendar.ShortDayLabel(day.Date),
        day.Entries.Count,
        day.Entries.Count == 0 ? "–" : DurationFormatter.Format(day.Total, format),
        day.Date == today,
        German.DateTimeFormat.GetDayName(day.Date.DayOfWeek),
        day.Date.ToString("dd.MM.", CultureInfo.InvariantCulture),
        BarRatioFor(day.Total, dailyTarget, weekMaxTotal));

    private static double BarRatioFor(TimeSpan total, TimeSpan dailyTarget, TimeSpan weekMaxTotal)
    {
        if (dailyTarget > TimeSpan.Zero)
        {
            return Math.Clamp(total.TotalHours / dailyTarget.TotalHours, 0.0, 1.0);
        }

        return weekMaxTotal > TimeSpan.Zero
            ? Math.Clamp(total.TotalHours / weekMaxTotal.TotalHours, 0.0, 1.0)
            : 0.0;
    }
}

/// <summary>Eine Tageszelle im Kalendergitter der Monatsansicht.</summary>
public sealed record MonthCellRow(
    DateOnly Date,
    string DayNumber,
    string TotalText,
    double BarRatio,
    bool IsToday,
    bool IsInMonth,
    bool HasEntries,
    bool IsWorkday)
{
    /// <summary>
    /// Eine Zelle, die zählt: im angezeigten Monat und entweder ein unter „Arbeitstage“ ausgewählter
    /// Wochentag oder ein abgewählter, an dem gebucht wurde. Alles andere steht ausgegraut da und
    /// lässt sich nicht anklicken.
    /// </summary>
    /// <remarks>
    /// Zur Ausnahme siehe WeekViewModel.IsVisible: Ein abgewählter Tag mit Buchung bleibt
    /// erreichbar, sonst käme man an seine Einträge nicht mehr heran, obwohl seine Zeit in der
    /// Wochen- und Monatssumme steckt.
    /// </remarks>
    public bool IsActive => IsInMonth && (IsWorkday || HasEntries);

    /// <summary><paramref name="monthAnchor"/> ist ein beliebiger Tag des angezeigten Kalendermonats.</summary>
    public static MonthCellRow From(
        DaySummary day,
        WorkSchedule schedule,
        DateOnly monthAnchor,
        DateOnly today,
        DurationFormat format = DurationFormat.HoursMinutes)
    {
        var dailyTarget = schedule.HoursFor(day.Date.DayOfWeek);
        var ratio = dailyTarget > TimeSpan.Zero
            ? Math.Clamp(day.Total.TotalHours / dailyTarget.TotalHours, 0.0, 1.0)
            : 0.0;

        return new MonthCellRow(
            day.Date,
            day.Date.Day.ToString(CultureInfo.InvariantCulture),
            day.Entries.Count == 0 ? "–" : DurationFormatter.Format(day.Total, format),
            ratio,
            day.Date == today,
            day.Date.Year == monthAnchor.Year && day.Date.Month == monthAnchor.Month,
            day.Entries.Count > 0,
            schedule.IsWorkday(day.Date.DayOfWeek));
    }
}

/// <summary>Eine Kalenderwochen-Zeile im Gitter der Monatsansicht: KW-Nummer, Wochensumme und sieben Tage.</summary>
public sealed record MonthWeekRow(int WeekNumber, string WeekTotalText, IReadOnlyList<MonthCellRow> Days);
