using System.Text.Json;

namespace Hora.Core.Jira;

/// <summary>Gemeinsame Serialisierungsoptionen: camelCase, Groß-/Kleinschreibung egal.</summary>
public static class JiraJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
