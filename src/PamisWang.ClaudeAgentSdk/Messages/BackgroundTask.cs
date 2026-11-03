using System.Text.Json;

namespace PamisWang.ClaudeAgentSdk.Messages;

/// <summary>一個在背景的任務，見 <see cref="BackgroundTasksChangedMessage.Tasks"/>。</summary>
public sealed record BackgroundTask
{
    /// <summary>任務 ID，用在 <see cref="ClaudeAgentClient.StopTaskAsync"/>。</summary>
    public required string TaskId { get; init; }

    /// <summary>任務的種類，例如 <c>local_agent</c>、<c>local_bash</c>。</summary>
    public required string TaskType { get; init; }

    /// <summary>任務的說明。</summary>
    public required string Description { get; init; }

    /// <summary>subagent 的類型。</summary>
    public string? SubagentType { get; init; }

    /// <summary>上層任務的 ID。</summary>
    public string? ParentTaskId { get; init; }

    /// <summary>原始的內容。</summary>
    public JsonElement Raw { get; init; }
}
