namespace Hora.Core.Everhour;

public class EverhourException : Exception
{
    public EverhourException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>HTTP 401: API-Schlüssel falsch oder in Everhour neu erzeugt.</summary>
public sealed class EverhourAuthenticationException : EverhourException
{
    public EverhourAuthenticationException()
        : base("Everhour hat den API-Schlüssel abgelehnt (HTTP 401).")
    {
    }
}

/// <summary>HTTP 429 auch nach einmaliger Wiederholung.</summary>
public sealed class EverhourRateLimitException : EverhourException
{
    public EverhourRateLimitException()
        : base("Everhour begrenzt gerade die Anfragen (HTTP 429).")
    {
    }
}

/// <summary>Netzwerkfehler, Zeitüberschreitung, HTTP 5xx, unerwartete 4xx oder eine möglicherweise abgeschnittene Antwort.</summary>
public sealed class EverhourUnavailableException : EverhourException
{
    public EverhourUnavailableException(string message, int? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public int? StatusCode { get; }
}
