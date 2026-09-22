using Hora.App.ViewModels;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

public class WorklogRowTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    private const string BaseUrl = "https://example.atlassian.net";

    [Fact]
    public void Time_range_shows_start_and_end_in_local_time()
    {
        var entry = WorklogEntry.Jira(
            "ABC-1",
            "Titel",
            new DateTimeOffset(2026, 9, 16, 9, 0, 0, TimeSpan.FromHours(2)),
            TimeSpan.FromMinutes(90),
            string.Empty,
            BaseUrl);

        var row = WorklogRow.From(entry, Berlin);

        Assert.Equal("09:00", row.StartText);
        Assert.Equal("09:00 – 10:30", row.TimeRangeText);
        Assert.Equal("1h 30m", row.DurationText);
    }

    [Fact]
    public void Time_range_crossing_the_spring_time_change_uses_the_offset_of_the_end()
    {
        // Befund H: Die Endzeit wurde im Offset des Startzeitpunkts gerechnet. Am 29.03.2026
        // springt die Uhr in Berlin um 02:00 auf 03:00 — eine Buchung von 01:30 über zwei Stunden
        // endet deshalb um 04:30 Ortszeit und wurde als "01:30 – 03:30" angezeigt.
        var entry = WorklogEntry.Jira(
            "ABC-1",
            "Titel",
            new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.FromHours(1)),
            TimeSpan.FromHours(2),
            string.Empty,
            BaseUrl);

        var row = WorklogRow.From(entry, Berlin);

        Assert.Equal("01:30", row.StartText);
        Assert.Equal("01:30 – 04:30", row.TimeRangeText);
        // Die Dauer bleibt davon unberührt: Betroffen war nur der Anzeigetext.
        Assert.Equal("2h", row.DurationText);
    }

    [Fact]
    public void Time_range_crossing_the_autumn_time_change_uses_the_offset_of_the_end()
    {
        // Gegenprobe zur Rückstellung: Am 25.10.2026 springt die Uhr in Berlin um 03:00 auf 02:00.
        // Eine Buchung ab 02:30 Sommerzeit (+02:00) über zwei Stunden endet um 03:30 Winterzeit.
        var entry = WorklogEntry.Jira(
            "ABC-1",
            "Titel",
            new DateTimeOffset(2026, 10, 25, 2, 30, 0, TimeSpan.FromHours(2)),
            TimeSpan.FromHours(2),
            string.Empty,
            BaseUrl);

        var row = WorklogRow.From(entry, Berlin);

        Assert.Equal("02:30", row.StartText);
        Assert.Equal("02:30 – 03:30", row.TimeRangeText);
        Assert.Equal("2h", row.DurationText);
    }
}
