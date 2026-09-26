using System.Text.Json;
using ClaudeTaskScheduler.Models;

namespace ClaudeTaskScheduler.Services;

/// <summary>
/// Simple JSON-file-backed CRUD store for task metadata. The Windows Task
/// Scheduler is the source of truth for whether a job actually runs; this
/// store just remembers what we asked it to create, plus display metadata.
/// </summary>
public sealed class TaskStore
{
    private readonly object _lock = new();
    private readonly List<ScheduledClaudeTask> _tasks;

    public TaskStore()
    {
        AppPaths.EnsureDirectories();
        _tasks = Load();
    }

    public IReadOnlyList<ScheduledClaudeTask> GetAll()
    {
        lock (_lock)
        {
            return _tasks.ToList();
        }
    }

    public ScheduledClaudeTask? Get(Guid id)
    {
        lock (_lock)
        {
            return _tasks.FirstOrDefault(t => t.Id == id);
        }
    }

    public void Add(ScheduledClaudeTask task)
    {
        lock (_lock)
        {
            _tasks.Add(task);
            Save();
        }
    }

    public bool Remove(Guid id)
    {
        lock (_lock)
        {
            var removed = _tasks.RemoveAll(t => t.Id == id) > 0;
            if (removed)
            {
                Save();
            }

            return removed;
        }
    }

    public void Update(ScheduledClaudeTask task)
    {
        lock (_lock)
        {
            var index = _tasks.FindIndex(t => t.Id == task.Id);
            if (index >= 0)
            {
                _tasks[index] = task;
                Save();
            }
        }
    }

    private List<ScheduledClaudeTask> Load()
    {
        if (!File.Exists(AppPaths.TasksFile))
        {
            return new List<ScheduledClaudeTask>();
        }

        var json = File.ReadAllText(AppPaths.TasksFile);
        return JsonSerializer.Deserialize<List<ScheduledClaudeTask>>(json, AppJson.Options) ?? new List<ScheduledClaudeTask>();
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_tasks, AppJson.Options);
        File.WriteAllText(AppPaths.TasksFile, json);
    }
}
