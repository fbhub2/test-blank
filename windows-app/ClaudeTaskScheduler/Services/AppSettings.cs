namespace ClaudeTaskScheduler.Services;

public sealed class AppSettings
{
    public int Port { get; set; } = 5055;

    /// <summary>Random per-install secret required via the X-Api-Key header.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Path (or bare command, if it is on PATH for the account that owns the
    /// scheduled tasks) used to invoke the Claude Code CLI.
    /// </summary>
    public string ClaudeExecutablePath { get; set; } = "claude";
}
