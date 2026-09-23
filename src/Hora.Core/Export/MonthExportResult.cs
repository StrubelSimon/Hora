namespace Hora.Core.Export;

/// <summary>
/// Dateiinhalt und die beiden Zahlen der Statuszeile in einem Rückgabewert: Die Profilseite soll
/// den fertigen Text nicht noch einmal auseinandernehmen müssen, um „152,25 h aus 21 Tagen“ zu
/// melden.
/// </summary>
public sealed record MonthExportResult(string Csv, decimal TotalHours, int DayCount);
