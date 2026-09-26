namespace ClaudeTaskScheduler.Models;

/// <summary>
/// Inbound payload for creating a task. <see cref="ScheduleType"/> is kept as a
/// raw string here so the endpoint can validate and report a friendly error
/// instead of failing model binding on a bad enum value.
/// </summary>
public sealed class CreateTaskRequest
{
    public string Name { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;

    public string? WorkingDirectory { get; set; }

    public string ScheduleType { get; set; } = "Once";

    public DateTime StartTime { get; set; }

    public int? IntervalMinutes { get; set; }

    public List<string>? DaysOfWeek { get; set; }
}

public sealed class ToggleRequest
{
    public bool Enabled { get; set; }
}
