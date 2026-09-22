using System.Globalization;

namespace Hora.Core.Jira;

/// <summary>
/// Parst Jira-Zeitstempel wie "2021-01-17T12:34:00.000+0000".
/// Jira schreibt den Offset ohne Doppelpunkt; das versteht System.Text.Json nicht.
/// </summary>
public static class JiraDateParser
{
    private static readonly string[] ExactFormats =
    {
        "yyyy-MM-dd'T'HH:mm:ss.FFFzzz",
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFF'Z'",
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
    };

    public static DateTimeOffset Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("Leerer Jira-Zeitstempel.");
        }

        var normalized = InsertColonIntoOffset(value.Trim());

        if (DateTimeOffset.TryParseExact(normalized, ExactFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var result))
        {
            return result;
        }

        throw new FormatException($"Ungültiger Jira-Zeitstempel: '{value}'.");
    }

    // "+0000" -> "+00:00". Werte mit Doppelpunkt oder "Z" bleiben unverändert.
    private static string InsertColonIntoOffset(string value)
    {
        if (value.Length < 5)
        {
            return value;
        }

        var tail = value[^5..];
        var looksLikeCompactOffset = (tail[0] == '+' || tail[0] == '-') && tail[1..].All(char.IsAsciiDigit);
        return looksLikeCompactOffset
            ? $"{value[..^5]}{tail[..3]}:{tail[3..]}"
            : value;
    }
}
