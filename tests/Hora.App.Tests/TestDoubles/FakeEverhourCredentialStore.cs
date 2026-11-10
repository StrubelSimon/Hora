using Hora.Core.Storage;

namespace Hora.App.Tests.TestDoubles;

internal sealed class FakeEverhourCredentialStore : IEverhourCredentialStore
{
    public EverhourCredentials? Stored { get; set; }

    public int DeleteCount { get; private set; }

    public void Save(EverhourCredentials credentials) => Stored = credentials;

    public EverhourCredentials? Load() => Stored;

    public void Delete()
    {
        DeleteCount++;
        Stored = null;
    }
}
