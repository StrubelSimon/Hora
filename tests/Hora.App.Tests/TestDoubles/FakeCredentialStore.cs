using Hora.Core.Storage;

namespace Hora.App.Tests.TestDoubles;

internal sealed class FakeCredentialStore : ICredentialStore
{
    public StoredCredentials? Stored { get; set; }

    public int DeleteCalls { get; private set; }

    /// <summary>Wird bei Load() geworfen, wenn gesetzt; standardmäßig inaktiv (Task 13, Fix-Runde 1).</summary>
    public Exception? LoadFailure { get; set; }

    public void Save(StoredCredentials credentials) => Stored = credentials;

    public StoredCredentials? Load() => LoadFailure is null ? Stored : throw LoadFailure;

    public void Delete()
    {
        DeleteCalls++;
        Stored = null;
    }
}
