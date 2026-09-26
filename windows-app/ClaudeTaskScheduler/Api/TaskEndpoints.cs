using System.Text.RegularExpressions;
using ClaudeTaskScheduler.Models;
using ClaudeTaskScheduler.Services;

namespace ClaudeTaskScheduler.Api;

public static class TaskEndpoints
{
    private static readonly HashSet<string> ValidDays = new(StringComparer.OrdinalIgnoreCase)
    {
        "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN",
    };

    // WorkingDirectory is interpolated into a generated .cmd file (inside
    // `cd /d "<path>"`). cmd.exe treats characters like & | < > ^ as command
    // separators/redirections *even inside a quoted string*, so a plain
    // quote-character check is not enough -- whitelist to a drive-rooted
    // Windows path built only from characters that cmd.exe cannot reinterpret.
    private static readonly Regex SafeWorkingDirectoryPattern = new(@"^[A-Za-z]:\\[A-Za-z0-9 ._\-\\]*$", RegexOptions.Compiled);

    public static void MapTaskEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api");

        group.MapGet("/ping", () => Results.Ok(new { status = "ok" }));

        group.MapGet("/tasks", (TaskStore store) => Results.Ok(store.GetAll()));

        group.MapPost("/tasks", async (CreateTaskRequest request, TaskStore store, WindowsTaskSchedulerService scheduler) =>
        {
            var validationError = Validate(request);
            if (validationError is not null)
            {
                return Results.BadRequest(new { error = validationError });
            }

            var task = new ScheduledClaudeTask
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Prompt = request.Prompt,
                WorkingDirectory = request.WorkingDirectory ?? string.Empty,
                ScheduleType = Enum.Parse<ScheduleType>(request.ScheduleType, ignoreCase: true),
                StartTime = request.StartTime,
                IntervalMinutes = request.IntervalMinutes,
                DaysOfWeek = request.DaysOfWeek ?? new List<string>(),
                Enabled = true,
            };

            try
            {
                await scheduler.CreateOrUpdateAsync(task);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }

            store.Add(task);
            return Results.Created($"/api/tasks/{task.Id}", task);
        });

        group.MapDelete("/tasks/{id:guid}", async (Guid id, TaskStore store, WindowsTaskSchedulerService scheduler) =>
        {
            var task = store.Get(id);
            if (task is null)
            {
                return Results.NotFound();
            }

            try
            {
                await scheduler.DeleteAsync(task);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }

            store.Remove(id);
            return Results.NoContent();
        });

        group.MapPost("/tasks/{id:guid}/run", async (Guid id, TaskStore store, WindowsTaskSchedulerService scheduler) =>
        {
            var task = store.Get(id);
            if (task is null)
            {
                return Results.NotFound();
            }

            try
            {
                await scheduler.RunNowAsync(task);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }

            task.LastTriggeredUtc = DateTime.UtcNow;
            store.Update(task);
            return Results.Ok(task);
        });

        group.MapPost("/tasks/{id:guid}/toggle", async (Guid id, ToggleRequest body, TaskStore store, WindowsTaskSchedulerService scheduler) =>
        {
            var task = store.Get(id);
            if (task is null)
            {
                return Results.NotFound();
            }

            try
            {
                await scheduler.SetEnabledAsync(task, body.Enabled);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }

            task.Enabled = body.Enabled;
            store.Update(task);
            return Results.Ok(task);
        });

        group.MapGet("/tasks/{id:guid}/log", (Guid id, TaskStore store, WindowsTaskSchedulerService scheduler) =>
        {
            var task = store.Get(id);
            if (task is null)
            {
                return Results.NotFound();
            }

            var logPath = scheduler.GetLogPath(task);
            if (!File.Exists(logPath))
            {
                return Results.Ok(new { log = string.Empty });
            }

            var lines = File.ReadAllLines(logPath);
            var tail = lines.Skip(Math.Max(0, lines.Length - 200));
            return Results.Ok(new { log = string.Join('\n', tail) });
        });
    }

    private static string? Validate(CreateTaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100)
        {
            return "Name is required and must be 100 characters or fewer.";
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return "Prompt is required.";
        }

        if (!string.IsNullOrEmpty(request.WorkingDirectory) &&
            (!Path.IsPathFullyQualified(request.WorkingDirectory) ||
             !SafeWorkingDirectoryPattern.IsMatch(request.WorkingDirectory)))
        {
            return "WorkingDirectory must be an absolute Windows path (e.g. C:\\Users\\you\\project) using only letters, digits, spaces, '.', '-', '_', and backslashes.";
        }

        if (!Enum.TryParse<ScheduleType>(request.ScheduleType, ignoreCase: true, out var scheduleType))
        {
            return "ScheduleType must be one of: Once, Daily, Weekly, Interval.";
        }

        switch (scheduleType)
        {
            case ScheduleType.Weekly:
                if (request.DaysOfWeek is null || request.DaysOfWeek.Count == 0 ||
                    request.DaysOfWeek.Any(d => !ValidDays.Contains(d)))
                {
                    return "DaysOfWeek must contain one or more of MON,TUE,WED,THU,FRI,SAT,SUN.";
                }

                break;

            case ScheduleType.Interval:
                if (request.IntervalMinutes is null || request.IntervalMinutes < 1 || request.IntervalMinutes > 1440)
                {
                    return "IntervalMinutes must be between 1 and 1440.";
                }

                break;
        }

        return null;
    }
}
