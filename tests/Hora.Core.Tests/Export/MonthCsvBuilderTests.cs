using Hora.Core.Export;
using Hora.Core.Worklogs;

namespace Hora.Core.Tests.Export;

/// <summary>
/// September 2026 als durchgehendes Beispiel: Der Monat beginnt an einem Dienstag, endet an einem
/// Mittwoch und hat 22 Werktage — bei acht Sollstunden also ein Monatssoll von 176 Stunden.
/// </summary>
public class MonthCsvBuilderTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    private static readonly TimeSpan Cest = TimeSpan.FromHours(2);
    private static readonly DateOnly September = new(2026, 9, 1);
    private static readonly DateTimeOffset GeneratedAt = new(2026, 9, 22, 17, 38, 0, Cest);

    private static WorklogEntry Entry(string issueKey, int day, int hour, int durationMinutes, string summary = "Titel") =>
        new(issueKey, summary, new DateTimeOffset(2026, 9, day, hour, 0, 0, Cest), TimeSpan.FromMinutes(durationMinutes), string.Empty);

    private static MonthExportResult Build(
        IReadOnlyList<WorklogEntry> entries,
        MonthExportOptions? options = null,
        WorkSchedule? schedule = null) =>
        MonthCsvBuilder.Build(
            entries,
            September,
            schedule ?? WorkSchedule.Default(),
            Berlin,
            options ?? MonthExportOptions.Default,
            "Simon Strubel",
            GeneratedAt);

    private static string[] Lines(MonthExportResult result) =>
        result.Csv.Split("\r\n", StringSplitOptions.None);

    // ===================== Kopfblock =====================

    [Fact]
    public void Header_names_the_user_the_month_and_the_moment_of_the_export()
    {
        var lines = Lines(Build(Array.Empty<WorklogEntry>()));

        Assert.Equal("Hora – Zeitexport", lines[0]);
        Assert.Equal("Mitarbeiter;Simon Strubel", lines[1]);
        Assert.Equal("Zeitraum;September 2026", lines[2]);
        Assert.Equal("Erstellt am;22.09.2026 17:38", lines[3]);
    }

    [Fact]
    public void Header_contrasts_the_months_workdays_with_the_days_that_actually_carry_bookings()
    {
        // Die beiden Zahlen sind der Ersatz für die weggelassenen leeren Tage: Ohne sie sähe ein
        // Monat mit vergessenen Tagen in der Datei aus wie ein vollständiger.
        var lines = Lines(Build(new[] { Entry("HORA-12", 1, 9, 480), Entry("HORA-12", 2, 9, 480) }));

        Assert.Equal("Arbeitstage im Monat;22", lines[4]);
        Assert.Equal("Tage mit Buchungen;2", lines[5]);
        Assert.Equal(string.Empty, lines[6]);
    }

    [Fact]
    public void Header_row_of_the_table_names_all_seven_columns()
    {
        var lines = Lines(Build(Array.Empty<WorklogEntry>()));

        Assert.Equal("Datum;Tag;Ticket;Beschreibung;Stunden;Soll;Diff", lines[7]);
    }

    // ===================== Tages- und Ticketzeilen =====================

    [Fact]
    public void A_day_row_carries_date_weekday_total_target_and_difference()
    {
        var result = Build(new[] { Entry("HORA-12", 1, 9, 330), Entry("HORA-7", 1, 15, 150) });

        Assert.Contains("01.09.2026;Di;;;8,00;8,00;0,00", result.Csv);
    }

    [Fact]
    public void An_issue_row_is_indented_by_two_spaces_and_stays_quoted()
    {
        var result = Build(new[] { Entry("HORA-12", 1, 9, 330, "Export-Funktion bauen") });

        Assert.Contains(";;\"  HORA-12\";Export-Funktion bauen;5,50;;", result.Csv);
    }

    [Fact]
    public void Several_bookings_on_one_issue_and_one_day_become_a_single_row()
    {
        var result = Build(new[]
        {
            Entry("HORA-12", 1, 9, 120),
            Entry("HORA-12", 1, 13, 90),
            Entry("HORA-12", 1, 16, 30),
        });

        var issueRows = Lines(result).Where(line => line.Contains("HORA-12")).ToList();
        Assert.Single(issueRows);
        Assert.Contains(";;\"  HORA-12\";Titel;4,00;;", issueRows[0]);
    }

    [Fact]
    public void Issues_of_a_day_are_sorted_by_hours_descending_then_by_key()
    {
        var result = Build(new[]
        {
            Entry("HORA-3", 1, 9, 60),
            Entry("HORA-99", 1, 11, 240),
            Entry("HORA-1", 1, 15, 60),
        });

        var keys = Lines(result)
            .Where(line => line.StartsWith(";;"))
            .Select(line => line.Split(';')[2].Trim('"').Trim())
            .ToList();

        Assert.Equal(new[] { "HORA-99", "HORA-1", "HORA-3" }, keys);
    }

    [Fact]
    public void Days_appear_in_ascending_order()
    {
        var result = Build(new[]
        {
            Entry("HORA-12", 17, 9, 60),
            Entry("HORA-12", 2, 9, 60),
            Entry("HORA-12", 9, 9, 60),
        });

        var days = Lines(result)
            .Where(line => line.StartsWith("0") || line.StartsWith("1"))
            .Select(line => line.Split(';')[0])
            .ToList();

        Assert.Equal(new[] { "02.09.2026", "09.09.2026", "17.09.2026" }, days);
    }

    [Fact]
    public void Days_without_bookings_get_no_row_at_all()
    {
        var result = Build(new[] { Entry("HORA-12", 1, 9, 480), Entry("HORA-12", 3, 9, 480) });

        Assert.DoesNotContain("02.09.2026", result.Csv);
        Assert.Equal(2, result.DayCount);
    }

    [Fact]
    public void A_saturday_with_bookings_shows_a_target_of_zero_and_a_positive_difference()
    {
        var result = Build(new[] { Entry("HORA-31", 5, 10, 180) });

        Assert.Contains("05.09.2026;Sa;;;3,00;0,00;3,00", result.Csv);
    }

    // ===================== Summenzeile =====================

    [Fact]
    public void Sum_row_counts_the_full_months_target_not_only_the_visible_rows()
    {
        // Ein einziger gebuchter Tag; das Soll der 21 übrigen Arbeitstage fehlt in der Tabelle,
        // muss aber in der Differenz auftauchen — sonst beschönigt die Datei den Monat.
        var result = Build(new[] { Entry("HORA-12", 1, 9, 480) });

        Assert.Contains("Summe;;;;8,00;176,00;-168,00", result.Csv);
    }

    [Fact]
    public void Sum_row_uses_an_ascii_hyphen_so_excel_still_reads_a_number()
    {
        var result = Build(new[] { Entry("HORA-12", 1, 9, 480) });

        Assert.DoesNotContain("−", result.Csv);
    }

    [Fact]
    public void Sum_row_is_the_last_line_of_the_file()
    {
        var lines = Lines(Build(new[] { Entry("HORA-12", 1, 9, 480) }));

        // Die Datei endet mit CRLF; hinter dem letzten Zeilenende steht ein leerer Rest.
        Assert.Equal(string.Empty, lines[^1]);
        Assert.StartsWith("Summe;", lines[^2]);
    }

    [Fact]
    public void Result_reports_the_total_hours_and_the_number_of_booked_days()
    {
        var result = Build(new[]
        {
            Entry("HORA-12", 1, 9, 480),
            Entry("HORA-7", 2, 9, 405),
        });

        Assert.Equal(14.75m, result.TotalHours);
        Assert.Equal(2, result.DayCount);
    }

    // ===================== Zahlen =====================

    [Fact]
    public void Hours_use_a_comma_and_two_decimals_even_when_they_are_whole()
    {
        var result = Build(new[] { Entry("HORA-12", 1, 9, 480) });

        Assert.Contains("8,00", result.Csv);
        Assert.DoesNotContain("8.00", result.Csv);
    }

    [Fact]
    public void A_day_total_is_rounded_once_at_the_end_not_per_issue()
    {
        // Drei Tickets zu je 20 Minuten: einzeln gerundet ergäbe die Spalte 0,33 + 0,33 + 0,33 =
        // 0,99. Die Tagessumme wird deshalb aus den ungerundeten Werten gebildet.
        var result = Build(new[]
        {
            Entry("HORA-1", 1, 9, 20),
            Entry("HORA-2", 1, 10, 20),
            Entry("HORA-3", 1, 11, 20),
        });

        Assert.Contains("01.09.2026;Di;;;1,00;8,00;-7,00", result.Csv);
        Assert.Equal(3, Lines(result).Count(line => line.Contains("0,33")));
    }

    // ===================== Zeitzone =====================

    [Fact]
    public void A_booking_shortly_before_midnight_stays_on_its_local_day()
    {
        var entries = new[]
        {
            new WorklogEntry("HORA-12", "Titel", new DateTimeOffset(2026, 9, 1, 23, 30, 0, Cest), TimeSpan.FromMinutes(60), string.Empty),
            new WorklogEntry("HORA-12", "Titel", new DateTimeOffset(2026, 9, 2, 0, 15, 0, Cest), TimeSpan.FromMinutes(60), string.Empty),
        };

        var result = Build(entries);

        Assert.Contains("01.09.2026;Di;;;1,00", result.Csv);
        Assert.Contains("02.09.2026;Mi;;;1,00", result.Csv);
    }

    [Fact]
    public void Bookings_outside_the_month_are_ignored()
    {
        var entries = new[]
        {
            Entry("HORA-12", 1, 9, 60),
            new WorklogEntry("HORA-99", "Titel", new DateTimeOffset(2026, 8, 31, 9, 0, 0, Cest), TimeSpan.FromMinutes(600), string.Empty),
            new WorklogEntry("HORA-98", "Titel", new DateTimeOffset(2026, 10, 1, 9, 0, 0, Cest), TimeSpan.FromMinutes(600), string.Empty),
        };

        var result = Build(entries);

        Assert.DoesNotContain("HORA-99", result.Csv);
        Assert.DoesNotContain("HORA-98", result.Csv);
        Assert.Equal(1m, result.TotalHours);
    }

    // ===================== Maskierung =====================

    [Fact]
    public void An_issue_title_with_separators_and_quotes_is_escaped()
    {
        var result = Build(new[] { Entry("HORA-12", 1, 9, 60, "Fehler in \"Suche\"; Umlaute") });

        Assert.Contains("\"Fehler in \"\"Suche\"\"; Umlaute\"", result.Csv);
    }

    // ===================== Schalter =====================

    [Fact]
    public void Without_issue_rows_only_the_day_rows_remain()
    {
        var result = Build(
            new[] { Entry("HORA-12", 1, 9, 330), Entry("HORA-7", 1, 15, 150) },
            new MonthExportOptions(IncludeIssueRows: false, IncludeTargets: true));

        Assert.Contains("01.09.2026;Di;;;8,00;8,00;0,00", result.Csv);
        Assert.DoesNotContain("HORA-12", result.Csv);
        Assert.DoesNotContain("HORA-7", result.Csv);
    }

    [Fact]
    public void Without_targets_the_last_two_columns_disappear_including_their_headers()
    {
        var result = Build(
            new[] { Entry("HORA-12", 1, 9, 330) },
            new MonthExportOptions(IncludeIssueRows: true, IncludeTargets: false));

        var lines = Lines(result);

        Assert.Equal("Datum;Tag;Ticket;Beschreibung;Stunden", lines[7]);
        Assert.Contains("01.09.2026;Di;;;5,50", lines);
        Assert.Contains("Summe;;;;5,50", lines);
        Assert.DoesNotContain("176,00", result.Csv);
    }

    [Fact]
    public void The_header_block_keeps_the_workday_count_even_without_the_target_columns()
    {
        // Die Zeile beschreibt den Monat, nicht die Spalte „Soll“.
        var result = Build(
            new[] { Entry("HORA-12", 1, 9, 330) },
            new MonthExportOptions(IncludeIssueRows: true, IncludeTargets: false));

        Assert.Contains("Arbeitstage im Monat;22", result.Csv);
    }

    // ===================== Leerer Monat =====================

    [Fact]
    public void An_empty_month_yields_nothing_but_the_header_and_a_zero_sum()
    {
        var result = Build(Array.Empty<WorklogEntry>());

        Assert.Equal(0, result.DayCount);
        Assert.Equal(0m, result.TotalHours);
        Assert.Contains("Tage mit Buchungen;0", result.Csv);
        Assert.Contains("Summe;;;;0,00;176,00;-176,00", result.Csv);
    }

    // ===================== Abweichende Arbeitswoche =====================

    [Fact]
    public void The_months_target_follows_the_configured_schedule()
    {
        // Drei Tage zu je sechs Stunden. Im September 2026 liegen vier Montage, fünf Mittwoche und
        // vier Freitage — also 13 Arbeitstage, macht 78 Stunden Monatssoll.
        var schedule = new WorkSchedule { Monday = 6, Wednesday = 6, Friday = 6 };

        var result = Build(new[] { Entry("HORA-12", 7, 9, 360) }, schedule: schedule);

        Assert.Contains("Arbeitstage im Monat;13", result.Csv);
        Assert.Contains("07.09.2026;Mo;;;6,00;6,00;0,00", result.Csv);
        Assert.Contains("Summe;;;;6,00;78,00;-72,00", result.Csv);
    }
}
