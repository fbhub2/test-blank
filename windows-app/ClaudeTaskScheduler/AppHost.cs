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
        // message loop (Application.Run in Program.cs) owns this thread. A
        // bind failure (e.g. the port already in use) would otherwise fail
        // silently, so at least capture it to a log file.
        _ = app.RunAsync().ContinueWith(
            t => TryLogHostFailure(t.Exception),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    public async Task StopAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
        }
    }

    private static void TryLogHostFailure(Exception? exception)
    {
        if (exception is null)
        {
            return;
        }

        try
        {
            AppPaths.EnsureDirectories();
            File.AppendAllText(
                Path.Combine(AppPaths.RootDir, "host-error.log"),
                $"{DateTime.UtcNow:o} {exception}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Best effort logging only.
        }
    }
}
