namespace ClaudeTaskScheduler.Services;

/// <summary>Well-known per-user storage locations under %LOCALAPPDATA%.</summary>
public static class AppPaths
{
    public static string RootDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClaudeTaskScheduler");

    public static string ConfigFile => Path.Combine(RootDir, "config.json");

    public static string TasksFile => Path.Combine(RootDir, "tasks.json");

    public static string ScriptsDir => Path.Combine(RootDir, "scripts");

    public static string LogsDir => Path.Combine(RootDir, "logs");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDir);
        Directory.CreateDirectory(ScriptsDir);
        Directory.CreateDirectory(LogsDir);
    }
}
