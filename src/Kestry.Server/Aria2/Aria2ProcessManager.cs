using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Kestry.Server.Settings;
using Microsoft.Extensions.Options;

namespace Kestry.Server.Aria2;

/// <summary>Starts aria2c as a child process, restarts it if it dies and shuts it down cleanly on stop.</summary>
public sealed class Aria2ProcessManager(
    Aria2Locator locator,
    Aria2Client client,
    Aria2Runtime runtime,
    SettingsService settings,
    IOptions<KestryOptions> kestry,
    IOptions<Aria2Options> aria2,
    TimeProvider time,
    ILogger<Aria2ProcessManager> log) : BackgroundService
{
    public static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(15);

    private Process? _process;

    private string SessionFile => Path.Combine(kestry.Value.DataDir, "aria2.session");

    public static bool PortIsFree(int port)
    {
        try
        {
            using var l = new TcpListener(IPAddress.Loopback, port);
            l.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    public string[] BuildArgs()
    {
        var s = settings.Current;
        var o = aria2.Value;
        var limited = s.SpeedLimitEnabled;
        var args = new List<string>
        {
            "--enable-rpc", "--rpc-listen-all=false", $"--rpc-listen-port={runtime.Port}", $"--rpc-secret={runtime.Secret}",
            $"--dir={s.DownloadDir}", $"--input-file={SessionFile}", $"--save-session={SessionFile}", "--save-session-interval=30",
            "--continue=true", $"--max-concurrent-downloads={s.MaxConcurrentDownloads}",
            $"--max-overall-download-limit={(limited ? s.DownloadLimitKBps : 0)}K",
            $"--max-overall-upload-limit={(limited ? s.UploadLimitKBps : 0)}K",
            "--follow-torrent=true", "--bt-save-metadata=true",
            $"--seed-ratio={(s.SeedingEnabled ? s.SeedRatio.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture) : "0")}",
            $"--listen-port={o.BtPort}", $"--dht-listen-port={o.BtPort}", "--enable-dht=true", "--bt-enable-lpd=true",
            "--enable-peer-exchange=true", "--quiet=true",
        };
        args.AddRange(o.ExtraArgs);
        return [.. args];
    }

    private void PrepareFiles()
    {
        Directory.CreateDirectory(kestry.Value.DataDir);
        Directory.CreateDirectory(settings.Current.DownloadDir);
        // Keep one backup of the session so a corrupted file never loses the queue.
        if (File.Exists(SessionFile)) File.Copy(SessionFile, SessionFile + ".bak", overwrite: true);
        else File.WriteAllText(SessionFile, "");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        runtime.Port = aria2.Value.Port;
        string exe;
        try
        {
            exe = await locator.ResolveAsync(() => runtime.SetState(Aria2State.Installing), stoppingToken);
        }
        catch (Aria2LocateException ex)
        {
            log.LogError("{Message}", ex.Message);
            runtime.SetState(Aria2State.Down, ex.Code, ex.Message);
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(exe, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "aria2 failed to start");
                runtime.SetState(Aria2State.Down, "crashed", ex.Message);
            }

            if (runtime.State != Aria2State.Down) runtime.SetState(Aria2State.Restarting);
            try
            {
                await Task.Delay(RestartDelay, time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task RunOnceAsync(string exe, CancellationToken ct)
    {
        if (!PortIsFree(runtime.Port))
        {
            var msg = $"RPC port {runtime.Port} is already in use. Free it or change Aria2:Port.";
            log.LogError("{Message}", msg);
            runtime.SetState(Aria2State.Down, "portBusy", msg);
            return;
        }

        ct.ThrowIfCancellationRequested();
        PrepareFiles();
        var psi = new ProcessStartInfo(exe) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var a in BuildArgs()) psi.ArgumentList.Add(a);
        var process = _process = Process.Start(psi) ?? throw new InvalidOperationException("Process.Start returned null");
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) log.LogDebug("aria2: {Line}", e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) log.LogWarning("aria2: {Line}", e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            try
            {
                using var startup = CancellationTokenSource.CreateLinkedTokenSource(ct);
                startup.CancelAfter(StartupTimeout);
                var version = await WaitForRpcAsync(process, startup.Token);
                runtime.SetState(Aria2State.Running);
                runtime.Version = version;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                runtime.SetState(Aria2State.Down, "crashed", "aria2 did not answer on its RPC port in time");
                Kill(process);
                return;
            }

            await process.WaitForExitAsync(ct);
            log.LogWarning("aria2 exited with code {Code}", process.ExitCode);
            runtime.SetState(Aria2State.Restarting);
        }
        finally
        {
            // On shutdown, save the session and stop aria2 gracefully; after any other failure, never leave it orphaned.
            if (!process.HasExited)
            {
                if (ct.IsCancellationRequested) await StopGracefullyAsync(process);
                else Kill(process);
            }
            _process = null;
            process.Dispose();
        }
    }

    private async Task<string> WaitForRpcAsync(Process process, CancellationToken ct)
    {
        while (true)
        {
            if (process.HasExited) throw new InvalidOperationException($"aria2 exited during startup (code {process.ExitCode})");
            try
            {
                return await client.GetVersionAsync(ct);
            }
            catch (HttpRequestException)
            {
                await Task.Delay(100, ct);
            }
        }
    }

    private static void Kill(Process p)
    {
        try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { /* already gone */ }
    }

    private async Task StopGracefullyAsync(Process p)
    {
        using var grace = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await client.SaveSessionAsync(grace.Token);
            await client.ShutdownAsync(grace.Token);
            await p.WaitForExitAsync(grace.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or Aria2Exception or OperationCanceledException or InvalidOperationException)
        {
            Kill(p);
        }
    }

    // Last resort when the container is disposed without a graceful stop (e.g. test hosts).
    public override void Dispose()
    {
        base.Dispose();
        if (_process is { HasExited: false } p) Kill(p);
    }
}
