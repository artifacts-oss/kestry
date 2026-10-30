using Kestry.Server;
using Kestry.Server.Aria2;
using Kestry.Server.Downloads;
using Kestry.Server.Security;
using Kestry.Server.Settings;
using Kestry.Server.Shutdown;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Requests are small JSON or a handful of .torrent files; anything bigger is refused early.
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = DownloadEndpoints.MaxRequestBytes);

builder.Services.Configure<KestryOptions>(builder.Configuration.GetSection("Kestry"));
builder.Services.Configure<Aria2Options>(builder.Configuration.GetSection("Aria2"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<Aria2Runtime>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddHttpClient<Aria2Client>(c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddSingleton(sp => new Aria2Locator(
    sp.GetRequiredService<IOptions<KestryOptions>>().Value.DataDir,
    sp.GetRequiredService<IOptions<Aria2Options>>().Value.Path,
    Environment.GetEnvironmentVariable("PATH"),
    OperatingSystem.IsWindows(),
    ct => Aria2Locator.DownloadPinnedAsync(sp.GetRequiredService<IHttpClientFactory>().CreateClient("aria2-download"), ct)));
builder.Services.AddHttpClient("aria2-download", c => c.Timeout = TimeSpan.FromMinutes(5));
builder.Services.AddSingleton<ISystemPower, SystemPower>();
builder.Services.AddSingleton(sp => new ShutdownService(
    sp.GetRequiredService<SettingsService>(),
    sp.GetRequiredService<IOptions<KestryOptions>>(),
    sp.GetRequiredService<ISystemPower>(),
    sp.GetRequiredService<Aria2Client>(),
    sp.GetRequiredService<Aria2Runtime>(),
    sp.GetRequiredService<TimeProvider>(),
    sp.GetRequiredService<ILogger<ShutdownService>>(),
    // The official .NET images set this variable; /.dockerenv covers other container builds.
    inContainer: Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" || File.Exists("/.dockerenv")));
builder.Services.AddSingleton<DownloadService>();
builder.Services.AddSingleton<SnapshotBuilder>();
builder.Services.AddSingleton<SnapshotHub>();
builder.Services.AddHostedService<DownloadMonitor>();
builder.Services.AddSingleton<Aria2ProcessManager>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Aria2ProcessManager>());

var app = builder.Build();

app.UseMiddleware<RequestGuard>();

app.MapGet("/health", (Aria2Runtime aria2) => Results.Json(new
{
    status = "ok",
    aria2 = aria2.State.ToString().ToLowerInvariant(),
}));

app.MapDownloadEndpoints();
app.MapEvents();
app.MapSettings();
app.MapShutdown();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

// Exposed so integration tests can use WebApplicationFactory<Program>.
public partial class Program;
