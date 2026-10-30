var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Json(new { status = "ok" }));

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

// Exposed so integration tests can use WebApplicationFactory<Program>.
public partial class Program;
