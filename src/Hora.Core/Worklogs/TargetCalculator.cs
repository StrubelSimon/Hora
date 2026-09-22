namespace Hora.Core.Worklogs;

/// <summary>Summiert die Sollzeit über einen Zeitraum. Feiertage und Urlaub bleiben unberücksichtigt.</summary>
public static class TargetCalculator
{
    public static TimeSpan ForRange(WorkSchedule schedule, DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            throw new ArgumentException("Das Enddatum liegt vor dem Startdatum.", nameof(to));
        }

        var total = TimeSpan.Zero;
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            total += schedule.HoursFor(date.DayOfWeek);
        }

        return total;
    }
}
