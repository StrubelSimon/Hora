using System.Text;

namespace Hora.Core.Export;

/// <summary>
/// Baut die Zeilen einer CSV-Datei. Trennzeichen ist das Semikolon: Es ist der Listentrenner des
/// deutschen Windows, und nur damit öffnet Excel die Datei per Doppelklick gleich in Spalten.
/// Zeilenende ist CRLF, weil die Datei auf Windows entsteht und dort gelesen wird.
/// </summary>
public sealed class CsvWriter
{
    public const char Separator = ';';

    private const string LineEnding = "\r\n";

    private readonly StringBuilder _lines = new();

    /// <summary>
    /// Setzt ein Feld in Anführungszeichen, sobald es sonst beim Einlesen zerfiele: Semikolon,
    /// Anführungszeichen, Zeilenumbruch — und Leerzeichen am Rand. Die letzten sind kein Sonderfall
    /// aus Vorsicht, sondern der Grund, warum die eingerückten Ticketnummern des Exports ihre
    /// Einrückung behalten: Ohne Anführungszeichen schneidet Excel sie beim Import ab.
    /// </summary>
    public static string Escape(string? field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return string.Empty;
        }

        var needsQuotes =
            field.Contains(Separator) ||
            field.Contains('"') ||
            field.Contains('\n') ||
            field.Contains('\r') ||
            char.IsWhiteSpace(field[0]) ||
            char.IsWhiteSpace(field[^1]);

        return needsQuotes ? $"\"{field.Replace("\"", "\"\"")}\"" : field;
    }

    public void WriteRow(params string?[] fields)
    {
        _lines.Append(string.Join(Separator, fields.Select(Escape))).Append(LineEnding);
    }

    /// <summary>Trennt den Kopfblock von der Tabelle.</summary>
    public void WriteBlankRow() => _lines.Append(LineEnding);

    public string Build() => _lines.ToString();
}
