using Hora.Core.Storage;

namespace Hora.App.Tests.TestDoubles;

internal sealed class FakeAsanaCredentialStore : IAsanaCredentialStore
{
    public AsanaCredentials? Stored { get; set; }

    public int DeleteCount { get; private set; }

    public void Save(AsanaCredentials credentials) => Stored = credentials;

    public AsanaCredentials? Load() => Stored;

    public void Delete()
    {
        DeleteCount++;
        Stored = null;
    }
}
