using System.Diagnostics;

namespace Kestry.Server.Shutdown;

/// <summary>Powers the machine off. Replaced by a double in tests: the real command must never run there.</summary>
public interface ISystemPower
{
    Task PowerOffAsync(CancellationToken ct);
}

public sealed class SystemPower : ISystemPower
{
    public async Task PowerOffAsync(CancellationToken ct)
    {
        var psi = OperatingSystem.IsWindows() ? new ProcessStartInfo("shutdown", "/s /t 0") : new ProcessStartInfo("systemctl", "poweroff");
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start the power-off command");
        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0) throw new InvalidOperationException($"{psi.FileName} exited with code {process.ExitCode}");
    }
}
