namespace ClaudeTaskScheduler.Models;

public enum ScheduleType
{
    Once,
    Daily,
    Weekly,
    Interval,
}

/// <summary>
/// A scheduled invocation of the Claude Code CLI, mirrored 1:1 with a job in
/// the Windows Task Scheduler (see <see cref="WindowsTaskPath"/>).
/// </summary>
public sealed class ScheduledClaudeTask
{
    public Guid Id { get; init; }

    public string Name { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public ScheduleType ScheduleType { get; set; }

    public DateTime StartTime { get; set; }

    public int? IntervalMinutes { get; set; }

    public List<string> DaysOfWeek { get; set; } = new();

    public bool Enabled { get; set; } = true;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastTriggeredUtc { get; set; }

    /// <summary>Name of the underlying Windows Task Scheduler task.</summary>
    public string WindowsTaskName => $"ClaudeTaskScheduler_{Id:N}";

    /// <summary>Fully qualified Task Scheduler path, kept in its own folder.</summary>
    public string WindowsTaskPath => $"\\ClaudeTaskScheduler\\{WindowsTaskName}";
}
