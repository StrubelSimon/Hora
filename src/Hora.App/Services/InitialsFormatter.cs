using System.Globalization;

namespace Hora.App.Services;

/// <summary>Leitet aus einem Anzeigenamen ein bis zwei Großbuchstaben-Initialen für den Avatar ab.</summary>
public static class InitialsFormatter
{
    public static string From(string displayName)
    {
        var words = displayName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return "?";
        }

        var first = char.ToUpper(words[0][0], CultureInfo.InvariantCulture);
        if (words.Length == 1)
        {
            return first.ToString();
        }

        var last = char.ToUpper(words[^1][0], CultureInfo.InvariantCulture);
        return $"{first}{last}";
    }
}
