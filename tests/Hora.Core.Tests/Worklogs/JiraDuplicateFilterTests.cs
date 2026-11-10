using Hora.Core.Worklogs;

namespace Hora.Core.Tests.Worklogs;

/// <summary>
/// Regel 3: Ein Asana-Spiegel eines Jira-Tickets zählt nicht, sobald Jira dasselbe Ticket am selben
/// Tag führt. Doppelt gezählt wird nie; im Zweifel wird ausgeblendet.
/// </summary>
public class JiraDuplicateFilterTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    private static readonly TimeSpan Winter = TimeSpan.FromHours(1);

    private static WorklogEntry Jira(string key, int day, int hour, int minute = 0) => WorklogEntry.Jira(
        key, key, new DateTimeOffset(2026, 11, day, hour, minute, 0, Winter),
        TimeSpan.FromHours(1), string.Empty, "https://advisori-ftc.atlassian.net");

    private static WorklogEntry Asana(string taskName, int day, double hours = 2) => new(
        WorklogSource.Asana, "Projekt", taskName,
        new DateTimeOffset(2026, 11, day, 0, 0, 0, Winter),
        TimeSpan.FromHours(hours), string.Empty, "https://app.asana.com/0/0/1/f", HasStartTime: false);

    private static JiraDuplicateResult Apply(
        IEnumerable<WorklogEntry> jira, IEnumerable<WorklogEntry> asana, params string[] failedIssueKeys) =>
        JiraDuplicateFilter.Apply(jira.ToList(), failedIssueKeys, asana.ToList(), Berlin);

    [Fact]
    public void HidesTheAsanaMirrorWhenJiraBookedTheSameTicketThatDay()
    {
        var mirror = Asana("SYN-2409 – Varda - Oberfläche auf Deutsch und Englisch umstellen", day: 3);

        var result = Apply(new[] { Jira("SYN-2409", day: 3, hour: 10) }, new[] { mirror });

        Assert.Empty(result.Kept);
        var hidden = Assert.Single(result.Hidden);
        Assert.Same(mirror, hidden.Entry);
        Assert.Equal("SYN-2409", hidden.TicketKey);
        Assert.Equal(new DateOnly(2026, 11, 3), hidden.Date);
    }

    /// <summary>Ohne Jira-Buchung an diesem Tag ist die Everhour-Zeit die einzige — sie bleibt.</summary>
    [Fact]
    public void KeepsTheMirrorWhenJiraBookedTheTicketOnAnotherDay()
    {
        var result = Apply(new[] { Jira("SYN-2409", day: 4, hour: 10) }, new[] { Asana("SYN-2409 – Varda", day: 3) });

        Assert.Single(result.Kept);
        Assert.Empty(result.Hidden);
    }

    [Fact]
    public void KeepsTasksWithoutTicketKey()
    {
        var result = Apply(new[] { Jira("SYN-2409", day: 3, hour: 10) }, new[] { Asana("Synthara - Strategie", day: 3) });

        Assert.Single(result.Kept);
    }

    /// <summary>23:30 Uhr Ortszeit ist in UTC schon 22:30 — der Tag muss trotzdem der lokale sein.</summary>
    [Fact]
    public void ComparesOnTheLocalDayLateInTheEvening()
    {
        var result = Apply(new[] { Jira("SYN-2409", day: 3, hour: 23, minute: 30) }, new[] { Asana("SYN-2409 – Varda", day: 3) });

        Assert.Single(result.Hidden);
    }

    /// <summary>00:30 Uhr Ortszeit am 4. liegt in UTC noch am 3. — und gehört trotzdem zum 4.</summary>
    [Fact]
    public void ComparesOnTheLocalDayJustAfterMidnight()
    {
        var result = Apply(new[] { Jira("SYN-2409", day: 4, hour: 0, minute: 30) }, new[] { Asana("SYN-2409 – Varda", day: 3) });

        Assert.Single(result.Kept);
    }

    [Fact]
    public void HidesWhenAnyOfSeveralKeysCollides()
    {
        var result = Apply(new[] { Jira("MAS-38", day: 3, hour: 9) }, new[] { Asana("SYN-2409 / MAS-38 Sammelticket", day: 3) });

        Assert.Equal("MAS-38", Assert.Single(result.Hidden).TicketKey);
    }

    [Theory]
    [InlineData("SYN-24 – anderes Ticket")]
    [InlineData("XSYN-2409 – anderes Projekt")]
    [InlineData("SYN-24090 – längere Nummer")]
    public void MatchesWholeKeysOnly(string taskName)
    {
        var result = Apply(new[] { Jira("SYN-2409", day: 3, hour: 9) }, new[] { Asana(taskName, day: 3) });

        Assert.Single(result.Kept);
    }

    /// <summary>
    /// Konnte Jira die Buchungen eines Tickets nicht laden, ist offen, ob dort gebucht ist. Im
    /// Zweifel wird ausgeblendet: Fehlt die Zeit, sieht man es; doppelt gezählt sieht man es nicht.
    /// </summary>
    [Fact]
    public void HidesWhenJiraCouldNotLoadTheTicket()
    {
        var result = Apply(Array.Empty<WorklogEntry>(), new[] { Asana("SYN-2409 – Varda", day: 3) }, "SYN-2409");

        Assert.Equal("SYN-2409", Assert.Single(result.Hidden).TicketKey);
    }

    [Fact]
    public void ReportsNothingWhenNothingWasHidden()
    {
        Assert.Null(JiraDuplicateFilter.FormatWarning(Array.Empty<HiddenAsanaEntry>()));
    }

    [Fact]
    public void WarningNamesTicketAndDay()
    {
        var result = Apply(new[] { Jira("SYN-2409", day: 3, hour: 10) }, new[] { Asana("SYN-2409 – Varda", day: 3) });

        Assert.Equal(
            "1 Everhour-Buchung ausgeblendet, weil das Ticket am selben Tag in Jira geführt wird: SYN-2409 (03.11.).",
            JiraDuplicateFilter.FormatWarning(result.Hidden));
    }

    [Fact]
    public void WarningCountsAllAndListsAtMostFive()
    {
        var jira = Enumerable.Range(1, 7).Select(day => Jira("SYN-1", day, hour: 9));
        var asana = Enumerable.Range(1, 7).Select(day => Asana("SYN-1 – Dauerläufer", day));

        var warning = JiraDuplicateFilter.FormatWarning(Apply(jira, asana).Hidden);

        Assert.Equal(
            "7 Everhour-Buchungen ausgeblendet, weil das Ticket am selben Tag in Jira geführt wird: " +
            "SYN-1 (01.11.), SYN-1 (02.11.), SYN-1 (03.11.), SYN-1 (04.11.), SYN-1 (05.11.) und 2 weitere.",
            warning);
    }
}
