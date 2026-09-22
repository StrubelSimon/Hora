namespace Hora.Core.Asana.Models;

/// <summary>Asana verpackt jede Antwort in `data`; bei Listen kommt `next_page` dazu.</summary>
public sealed class AsanaResponse<T>
{
    public T? Data { get; set; }

    public AsanaNextPage? NextPage { get; set; }
}

/// <summary>Zeiger auf die nächste Seite; null bedeutet, dass das Ende erreicht ist.</summary>
public sealed class AsanaNextPage
{
    public string Offset { get; set; } = string.Empty;

    public string? Path { get; set; }

    public string? Uri { get; set; }
}
