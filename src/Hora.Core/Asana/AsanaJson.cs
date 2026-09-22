using System.Text.Json;

namespace Hora.Core.Asana;

/// <summary>
/// Asana liefert snake_case (`duration_minutes`, `entered_on`, `next_page`). `JsonSerializerDefaults.Web`
/// allein reicht deshalb nicht — es erwartet camelCase. Die Benennungsregel hier übersetzt beides.
/// </summary>
public static class AsanaJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
