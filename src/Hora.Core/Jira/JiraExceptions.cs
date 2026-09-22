namespace Hora.Core.Jira;

public class JiraException : Exception
{
    public JiraException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>HTTP 401 oder 403: Zugangsdaten falsch, abgelaufen oder widerrufen.</summary>
public sealed class JiraAuthenticationException : JiraException
{
    public JiraAuthenticationException()
        : base("Jira hat die Anmeldung abgelehnt (HTTP 401/403).")
    {
    }
}

/// <summary>HTTP 429 auch nach einmaliger Wiederholung.</summary>
public sealed class JiraRateLimitException : JiraException
{
    public JiraRateLimitException()
        : base("Jira begrenzt gerade die Anfragen (HTTP 429).")
    {
    }
}

/// <summary>Netzwerkfehler, Zeitüberschreitung, HTTP 5xx oder unerwartete 4xx.</summary>
public sealed class JiraUnavailableException : JiraException
{
    public JiraUnavailableException(string message, int? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public int? StatusCode { get; }
}
