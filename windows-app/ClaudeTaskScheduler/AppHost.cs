using System.Text.Json.Serialization;
using ClaudeTaskScheduler.Api;
using ClaudeTaskScheduler.Services;

namespace ClaudeTaskScheduler;

/// <summary>
/// Owns the embedded Kestrel web host (REST API + mobile web page) that runs
/// alongside the tray icon for the lifetime of the process.
/// </summary>
public sealed class AppHost
{
    private WebApplication? _app;

    public string BaseUrl { get; private set; } = string.Empty;

    public string ApiKey { get; private set; } = string.Empty;

    public void Start()
    {
        var settings = AppSettingsService.LoadOrCreate();
        ApiKey = settings.ApiKey;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://0.0.0.0:{settings.Port}/");

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton<TaskStore>();
        builder.Services.AddSingleton<WindowsTaskSchedulerService>();

        var app = builder.Build();

        app.UseMiddleware<ApiKeyMiddleware>();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapTaskEndpoints();

        _app = app;
        BaseUrl = $"http://localhost:{settings.Port}/";

        // Fire-and-forget: Kestrel runs on its own threads while the WinForms
        // message loop (Application.Run in Program.cs) owns this thread.
        _ = app.RunAsync();
    }

    public async Task StopAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
        }
    }
}
