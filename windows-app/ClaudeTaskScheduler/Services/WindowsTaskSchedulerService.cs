using System.Diagnostics;
using System.Text;
using ClaudeTaskScheduler.Models;

namespace ClaudeTaskScheduler.Services;

/// <summary>
/// Creates/updates/deletes the actual Windows Task Scheduler jobs backing a
/// <see cref="ScheduledClaudeTask"/>, by shelling out to schtasks.exe.
///
/// Every task's action is a generated .cmd wrapper script (never a raw
/// command line built from user input) so that arbitrary prompt text can
/// never break out of quoting: the prompt is written verbatim to its own
/// file and piped into the Claude CLI over stdin.
/// </summary>
public sealed class WindowsTaskSchedulerService
{
    private static readonly string[] ValidDays = { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" };

    private readonly AppSettings _settings;

    public WindowsTaskSchedulerService(AppSettings settings)
    {
        _settings = settings;
    }

    public async Task CreateOrUpdateAsync(ScheduledClaudeTask task)
    {
        var scriptPath = WriteWrapperScript(task);
        var args = BuildCreateArguments(task, scriptPath);
        await RunSchtasksAsync(args);
    }

    public async Task DeleteAsync(ScheduledClaudeTask task)
    {
        await RunSchtasksAsync(new[] { "/delete", "/tn", task.WindowsTaskPath, "/f" });
        DeleteWrapperFiles(task);
    }

    public async Task RunNowAsync(ScheduledClaudeTask task)
    {
        await RunSchtasksAsync(new[] { "/run", "/tn", task.WindowsTaskPath });
    }

    public async Task SetEnabledAsync(ScheduledClaudeTask task, bool enabled)
    {
        await RunSchtasksAsync(new[] { "/change", "/tn", task.WindowsTaskPath, enabled ? "/enable" : "/disable" });
    }

    public string GetLogPath(ScheduledClaudeTask task) => Path.Combine(AppPaths.LogsDir, $"{task.Id:N}.log");

    private string WriteWrapperScript(ScheduledClaudeTask task)
    {
        AppPaths.EnsureDirectories();

        var promptPath = Path.Combine(AppPaths.ScriptsDir, $"{task.Id:N}.prompt.txt");
        var scriptPath = Path.Combine(AppPaths.ScriptsDir, $"{task.Id:N}.cmd");
        var logPath = GetLogPath(task);

        // Written as a plain file, never interpolated into a shell command,
        // so the prompt content cannot inject anything into the script.
        File.WriteAllText(promptPath, task.Prompt);

        var workingDir = string.IsNullOrWhiteSpace(task.WorkingDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : task.WorkingDirectory;

        var script = new StringBuilder();
        script.AppendLine("@echo off");
        script.AppendLine($"cd /d \"{workingDir}\"");
        script.AppendLine($"echo ---- run start %date% %time% ---- >> \"{logPath}\"");
        script.AppendLine($"\"{_settings.ClaudeExecutablePath}\" -p < \"{promptPath}\" >> \"{logPath}\" 2>&1");
        script.AppendLine($"echo ---- run end %date% %time% (exit %errorlevel%) ---- >> \"{logPath}\"");

        File.WriteAllText(scriptPath, script.ToString());
        return scriptPath;
    }

    private static void DeleteWrapperFiles(ScheduledClaudeTask task)
    {
        TryDelete(Path.Combine(AppPaths.ScriptsDir, $"{task.Id:N}.prompt.txt"));
        TryDelete(Path.Combine(AppPaths.ScriptsDir, $"{task.Id:N}.cmd"));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup; a locked file is not fatal.
        }
    }

    private static string[] BuildCreateArguments(ScheduledClaudeTask task, string scriptPath)
    {
        var args = new List<string>
        {
            "/create",
            "/tn", task.WindowsTaskPath,

            // The value schtasks stores as the task's action must itself be
            // quoted when the path contains spaces (e.g. "Local App Data" on
            // some machines) -- this is a literal quote *inside* the string,
            // distinct from how ArgumentList quotes it for this process launch.
            "/tr", $"\"{scriptPath}\"",
            "/f",
        };

        switch (task.ScheduleType)
        {
            case ScheduleType.Once:
                args.AddRange(new[]
                {
                    "/sc", "ONCE",
                    "/sd", task.StartTime.ToString("MM/dd/yyyy"),
                    "/st", task.StartTime.ToString("HH:mm"),
                });
                break;

            case ScheduleType.Daily:
                args.AddRange(new[] { "/sc", "DAILY", "/st", task.StartTime.ToString("HH:mm") });
                break;

            case ScheduleType.Weekly:
                var days = task.DaysOfWeek.Where(d => ValidDays.Contains(d, StringComparer.OrdinalIgnoreCase));
                args.AddRange(new[] { "/sc", "WEEKLY", "/d", string.Join(",", days), "/st", task.StartTime.ToString("HH:mm") });
                break;

            case ScheduleType.Interval:
                args.AddRange(new[] { "/sc", "MINUTE", "/mo", (task.IntervalMinutes ?? 60).ToString() });
                break;
        }

        return args.ToArray();
    }

    private static async Task RunSchtasksAsync(IEnumerable<string> arguments)
    {
        var psi = new ProcessStartInfo("schtasks.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start schtasks.exe");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"schtasks.exe failed with exit code {process.ExitCode}: {stderr}{stdout}".Trim());
        }
    }
}
