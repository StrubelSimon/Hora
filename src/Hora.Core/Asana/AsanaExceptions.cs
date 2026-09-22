namespace Hora.Core.Asana;

public class AsanaException : Exception
{
    public AsanaException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>HTTP 401: Token falsch oder widerrufen.</summary>
public sealed class AsanaAuthenticationException : AsanaException
{
    public AsanaAuthenticationException()
        : base("Asana hat den Token abgelehnt (HTTP 401).")
    {
    }
}

/// <summary>
/// HTTP 402 oder 403 auf dem Zeiteintrags-Endpunkt: Der Tarif enthält keine Zeiterfassung.
/// Bewusst von der Anmeldung getrennt — der Token ist gültig, nur die Funktion fehlt. Ein
/// Löschen der Verbindung wäre hier falsch, der Nutzer soll sie behalten und den Tarif klären.
/// </summary>
public sealed class AsanaFeatureUnavailableException : AsanaException
{
    public AsanaFeatureUnavailableException()
        : base("Dieser Asana-Tarif enthält keine Zeiterfassung (HTTP 402/403).")
    {
    }
}

/// <summary>HTTP 429 auch nach einmaliger Wiederholung.</summary>
public sealed class AsanaRateLimitException : AsanaException
{
    public AsanaRateLimitException()
        : base("Asana begrenzt gerade die Anfragen (HTTP 429).")
    {
    }
}

/// <summary>Netzwerkfehler, Zeitüberschreitung, HTTP 5xx oder unerwartete 4xx.</summary>
public sealed class AsanaUnavailableException : AsanaException
{
    public AsanaUnavailableException(string message, int? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public int? StatusCode { get; }
}
