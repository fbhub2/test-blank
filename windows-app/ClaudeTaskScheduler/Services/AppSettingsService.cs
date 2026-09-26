using System.Security.Cryptography;
using System.Text.Json;

namespace ClaudeTaskScheduler.Services;

public static class AppSettingsService
{
    public static AppSettings LoadOrCreate()
    {
        AppPaths.EnsureDirectories();

        if (File.Exists(AppPaths.ConfigFile))
        {
            var existing = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.ConfigFile), AppJson.Options);
            if (existing is not null && !string.IsNullOrEmpty(existing.ApiKey))
            {
                return existing;
            }
        }

        var settings = new AppSettings
        {
            ApiKey = GenerateApiKey(),
        };

        Save(settings);
        return settings;
    }

    public static void Save(AppSettings settings)
    {
        AppPaths.EnsureDirectories();
        File.WriteAllText(AppPaths.ConfigFile, JsonSerializer.Serialize(settings, AppJson.Options));
    }

    private static string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
