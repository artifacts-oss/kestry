using Kestry.Server.Shutdown;

namespace Kestry.Server.Tests;

/// <summary>Records power-off requests instead of running the real command.</summary>
public sealed class FakePower : ISystemPower
{
    public int Calls;
    public Exception? Fail;

    public Task PowerOffAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref Calls);
        return Fail is null ? Task.CompletedTask : Task.FromException(Fail);
    }
}
