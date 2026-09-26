using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeTaskScheduler.Services;

/// <summary>Shared JSON options for the on-disk config/tasks files.</summary>
public static class AppJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
