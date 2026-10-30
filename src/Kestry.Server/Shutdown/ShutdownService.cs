using Kestry.Server.Aria2;
using Kestry.Server.Downloads;
using Kestry.Server.Settings;
using Microsoft.Extensions.Options;

namespace Kestry.Server.Shutdown;

/// <summary>
/// "Shut down when finished": once the queue empties after at least one download completed, starts a countdown
/// that every client can see and cancel, then powers the machine off.
/// </summary>
public sealed class ShutdownService(
    SettingsService settings,
    IOptions<KestryOptions> kestry,
    ISystemPower power,
    Aria2Client aria2,
    Aria2Runtime runtime,
    TimeProvider time,
    ILogger<ShutdownService> log,
    bool inContainer)
{
    public static readonly TimeSpan Countdown = TimeSpan.FromSeconds(60);

    private readonly object _gate = new();
    private Dictionary<string, string> _previous = [];
    private bool _completedThisSession;
    private DateTimeOffset? _endsAt;
    private bool _fired;

    /// <summary>null when available; otherwise why not: disabled | container | unsupported.</summary>
    public string? UnavailableReason =>
        !kestry.Value.AllowShutdown ? "disabled"
        : inContainer ? "container"
        : !(OperatingSystem.IsWindows() || OperatingSystem.IsLinux()) ? "unsupported"
        : null;

    public bool Available => UnavailableReason is null;

    public int? SecondsRemaining
    {
        get
        {
            lock (_gate)
                return _endsAt is { } end ? Math.Max(0, (int)Math.Ceiling((end - time.GetUtcNow()).TotalSeconds)) : null;
        }
    }

    /// <summary>Called on every monitor tick with the current downloads.</summary>
    public void Evaluate(IReadOnlyList<DownloadDto> downloads, bool aria2Running)
    {
        lock (_gate)
        {
            if (downloads.Any(d => d.Status == "completed" && _previous.TryGetValue(d.Gid, out var was) && was != "completed")) _completedThisSession = true;
            _previous = downloads.ToDictionary(d => d.Gid, d => d.Status);

            // Paused downloads and torrents still seeding count as pending work: never power off under them.
            var pending = downloads.Any(d => d.Status is "downloading" or "queued" or "paused" || d.Seeding);
            var due = Available && settings.Current.ShutdownWhenDone && aria2Running && _completedThisSession && !pending && !_fired;
            if (!due)
            {
                _endsAt = null;
                return;
            }

            var now = time.GetUtcNow();
            _endsAt ??= now + Countdown;
            if (now < _endsAt) return;
            _fired = true;
        }
        _ = PowerOffAsync();
    }

    /// <summary>Aborts the countdown; it will not restart until another download completes.</summary>
    public void Cancel()
    {
        lock (_gate)
        {
            _endsAt = null;
            _completedThisSession = false;
        }
    }

    private async Task PowerOffAsync()
    {
        try
        {
            log.LogWarning("Queue finished: powering off");
            await aria2.SaveSessionAsync();
            await power.PowerOffAsync(CancellationToken.None);
            runtime.ShuttingDown = true; // the OS is going down: do not restart aria2
            await aria2.ShutdownAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or Aria2Exception or System.ComponentModel.Win32Exception)
        {
            log.LogError(ex, "Could not power off");
        }
        finally
        {
            lock (_gate)
            {
                _endsAt = null;
                _completedThisSession = false;
                _fired = false;
            }
        }
    }
}
