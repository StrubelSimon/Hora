namespace Hora.Core.Storage;

/// <summary>Zugangsdaten, die verschlüsselt auf der Platte liegen.</summary>
public sealed record StoredCredentials(string BaseUrl, string Email, string ApiToken);
