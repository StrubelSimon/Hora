using Hora.App.ViewModels;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

/// <summary>
/// Abgewählte Wochentage gehören nicht in die Wochenansicht und stehen in der Monatsansicht
/// ausgegraut da. Maßgeblich ist allein die Auswahl „Arbeitstage“ der Profilseite, nicht die
/// Annahme, das Wochenende sei Samstag und Sonntag.
/// </summary>
public class WorkdayVisibilityTests
{
    private static readonly DateOnly Monday = new(2026, 9, 14); // Montag, KW 38
    private static readonly DateOnly Saturday = new(2026, 9, 19);
    private static readonly DateOnly Sunday = new(2026, 9, 20);

    private const string BaseUrl = "https://example.atlassian.net";

    private static WorkSchedule MondayToFriday() => WorkSchedule.Default();

    private static WorklogEntry Entry(DateOnly date) =>
        WorklogEntry.Jira("ABC-1", "Titel", new DateTimeOffset(date.Year, date.Month, date.Day, 9, 0, 0, TimeSpan.FromHours(2)),
            TimeSpan.FromHours(2), string.Empty, BaseUrl);

    private static DaySummary EmptyDay(DateOnly date) =>
        new(date, Array.Empty<WorklogEntry>(), TimeSpan.Zero);

    private static DaySummary BookedDay(DateOnly date) =>
        new(date, new[] { Entry(date) }, TimeSpan.FromHours(2));

    /// <summary>Eine volle Woche ab Montag; an den genannten Tagen steht je eine Buchung.</summary>
    private static WeekSummary Week(params DateOnly[] daysWithEntries)
    {
        var days = Enumerable.Range(0, 7)
            .Select(offset => Monday.AddDays(offset))
            .Select(date => daysWithEntries.Contains(date) ? BookedDay(date) : EmptyDay(date))
            .ToList();

        return new WeekSummary(Monday, days, days.Aggregate(TimeSpan.Zero, (sum, day) => sum + day.Total));
    }

    // ===================== Wochenansicht =====================

    [Fact]
    public void Week_view_omits_days_that_are_not_workdays()
    {
        var vm = new WeekViewModel(MondayToFriday());

        vm.Show(Week(), Array.Empty<IssueTotal>(), Monday);

        Assert.Equal(
            new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
            vm.Days.Select(row => row.Date.DayOfWeek));
    }

    [Fact]
    public void Week_view_keeps_a_non_workday_that_carries_a_booking()
    {
        // Andernfalls verschwände gebuchte Zeit aus der Liste, die in der Wochensumme trotzdem steckt.
        var vm = new WeekViewModel(MondayToFriday());

        vm.Show(Week(Saturday), Array.Empty<IssueTotal>(), Monday);

        Assert.Equal(6, vm.Days.Count);
        Assert.Contains(vm.Days, row => row.Date == Saturday);
        Assert.DoesNotContain(vm.Days, row => row.Date == Sunday);
    }

    [Fact]
    public void Week_view_follows_a_schedule_made_of_saturday_and_sunday()
    {
        var schedule = new WorkSchedule { Saturday = 8, Sunday = 8 };
        var vm = new WeekViewModel(schedule);

        vm.Show(Week(), Array.Empty<IssueTotal>(), Monday);

        Assert.Equal(new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }, vm.Days.Select(row => row.Date.DayOfWeek));
    }

    // ===================== Monatsansicht =====================

    [Fact]
    public void Month_cell_of_a_deselected_weekday_is_inactive()
    {
        var cell = MonthCellRow.From(EmptyDay(Saturday), MondayToFriday(), Saturday, Monday);

        Assert.False(cell.IsWorkday);
        Assert.False(cell.IsActive);
    }

    [Fact]
    public void Month_cell_of_a_workday_is_active()
    {
        var cell = MonthCellRow.From(EmptyDay(Monday), MondayToFriday(), Monday, Monday);

        Assert.True(cell.IsWorkday);
        Assert.True(cell.IsActive);
    }

    [Fact]
    public void Month_cell_of_a_deselected_weekday_with_a_booking_stays_active()
    {
        var cell = MonthCellRow.From(BookedDay(Saturday), MondayToFriday(), Saturday, Monday);

        Assert.False(cell.IsWorkday);
        Assert.True(cell.IsActive);
    }

    [Fact]
    public void Month_cell_outside_the_month_stays_inactive()
    {
        // Gegenprobe: An Tagen des Nachbarmonats ändert der Arbeitstag nichts.
        var cell = MonthCellRow.From(BookedDay(Monday), MondayToFriday(), new DateOnly(2026, 10, 1), Monday);

        Assert.True(cell.IsWorkday);
        Assert.False(cell.IsActive);
    }

    [Fact]
    public void Month_view_ignores_a_click_on_a_deselected_weekday()
    {
        var vm = new MonthViewModel(MondayToFriday());
        var visibleDays = Enumerable.Range(0, 7).Select(offset => EmptyDay(Monday.AddDays(offset))).ToList();
        vm.Show(visibleDays, Monday, Monday, Array.Empty<IssueTotal>());

        var selected = new List<DateOnly>();
        vm.DaySelected += date => selected.Add(date);

        foreach (var cell in vm.Weeks.Single().Days)
        {
            vm.SelectDayCommand.Execute(cell);
        }

        Assert.Equal(5, selected.Count);
        Assert.DoesNotContain(Saturday, selected);
        Assert.DoesNotContain(Sunday, selected);
    }
}
