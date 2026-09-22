using System.Text;
using System.Text.Json;

namespace Hora.Core.Jira;

/// <summary>Extrahiert Klartext aus einem Kommentar im Atlassian Document Format (ADF).</summary>
public static class AdfTextExtractor
{
    public static string Extract(JsonElement? document)
    {
        if (document is null)
        {
            return string.Empty;
        }

        var root = document.Value;

        // Sehr alte Worklogs tragen den Kommentar noch als einfachen String.
        if (root.ValueKind == JsonValueKind.String)
        {
            return root.GetString() ?? string.Empty;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        var lines = new List<string>();
        var currentLine = new StringBuilder();
        Walk(root, currentLine, lines);
        FlushLine(currentLine, lines);
        return string.Join(Environment.NewLine, lines);
    }

    private static void Walk(JsonElement node, StringBuilder currentLine, List<string> lines)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var type = node.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;

        switch (type)
        {
            case "text":
                if (node.TryGetProperty("text", out var text))
                {
                    currentLine.Append(text.GetString());
                }
                return;

            case "mention":
            case "emoji":
                if (node.TryGetProperty("attrs", out var attrs) && attrs.TryGetProperty("text", out var attrText))
                {
                    currentLine.Append(attrText.GetString());
                }
                return;

            case "hardBreak":
                FlushLine(currentLine, lines);
                return;
        }

        if (node.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in content.EnumerateArray())
            {
                Walk(child, currentLine, lines);
            }
        }

        // Blockelemente beenden eine Zeile.
        if (type is "paragraph" or "heading" or "codeBlock" or "blockquote" or "listItem")
        {
            FlushLine(currentLine, lines);
        }
    }

    private static void FlushLine(StringBuilder currentLine, List<string> lines)
    {
        var line = currentLine.ToString().Trim();
        currentLine.Clear();
        if (line.Length > 0)
        {
            lines.Add(line);
        }
    }
}
