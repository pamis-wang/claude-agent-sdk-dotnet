namespace PamisWang.ClaudeAgentSdk.Messages;

/// <summary>
/// 一個任務開始了（<c>system</c>／<c>task_started</c>），例如交辦給 subagent 的工作或背景執行的指令。對應 Python SDK 的 <c>TaskStartedMessage</c>。
/// 是 <see cref="SystemMessage"/> 的子型別，原本用 <see cref="SystemMessage.Subtype"/> 判斷的程式不受影響。
/// </summary>
public sealed record TaskStartedMessage : SystemMessage
{
    /// <summary>任務 ID，用在 <see cref="ClaudeAgentClient.StopTaskAsync"/>。</summary>
    public required string TaskId { get; init; }

    /// <summary>任務的說明。</summary>
    public required string Description { get; init; }

    /// <summary>啟動這個任務的工具呼叫 ID。</summary>
    public string? ToolUseId { get; init; }

    /// <summary>任務的種類，例如 <c>local_agent</c>（subagent）、<c>local_bash</c>（背景指令）。</summary>
    public string? TaskType { get; init; }

    /// <summary>subagent 的類型。Python SDK 沒有這個欄位，對應 TypeScript SDK 的 <c>subagent_type</c>。</summary>
    public string? SubagentType { get; init; }

    /// <summary>是不是在背景執行。Python SDK 沒有這個欄位，對應 TypeScript SDK 的 <c>is_backgrounded</c>。</summary>
    public bool? IsBackgrounded { get; init; }

    /// <summary>上層任務的 ID（任務裡又開的任務）。Python SDK 沒有這個欄位，對應 TypeScript SDK 的 <c>parent_task_id</c>。</summary>
    public string? ParentTaskId { get; init; }

    /// <summary>交辦給 subagent 的內容。Python SDK 沒有這個欄位，對應 TypeScript SDK 的 <c>prompt</c>。</summary>
    public string? Prompt { get; init; }
}
