using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kestry.Server.Tests;

/// <summary>Hosts Kestry with its data, downloads and aria2 ports isolated in a temp folder.</summary>
public sealed class KestryFactory : WebApplicationFactory<Program>
{
    private bool _stopped;

    public string Root { get; } = Directory.CreateTempSubdirectory("kestry-it").FullName;
    public string DownloadDir => Path.Combine(Root, "downloads");
    public int Aria2Port { get; } = FreePort();

    public static int FreePort()
    {
        using var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        return ((IPEndPoint)l.LocalEndpoint).Port;
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kestry:DataDir"] = Path.Combine(Root, "data"),
            ["Kestry:AllowedRoots:0"] = DownloadDir,
            ["Aria2:Port"] = Aria2Port.ToString(),
            ["Aria2:BtPort"] = FreePort().ToString(),
        }));

    protected override void Dispose(bool disposing)
    {
        // Stop gracefully so the aria2 child process never outlives the test.
        if (disposing && !_stopped)
        {
            _stopped = true;
            Services.GetService<IHost>()?.StopAsync().GetAwaiter().GetResult();
        }
        base.Dispose(disposing);
        if (disposing) try { Directory.Delete(Root, recursive: true); } catch (IOException) { /* aria2 may still hold a file */ }
    }
}
