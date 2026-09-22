using Hora.Core.Logging;

namespace Hora.App.Tests.TestDoubles;

internal sealed class NullLogger : IAppLogger
{
    public void Info(string message)
    {
    }

    public void Error(string message, Exception? exception = null)
    {
    }
}
