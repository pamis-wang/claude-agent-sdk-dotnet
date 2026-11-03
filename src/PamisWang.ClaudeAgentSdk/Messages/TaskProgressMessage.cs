namespace PamisWang.ClaudeAgentSdk.Messages;

/// <summary>任務執行中的進度（<c>system</c>／<c>task_progress</c>）。對應 Python SDK 的 <c>TaskProgressMessage</c>。</summary>
public sealed record TaskProgressMessage : SystemMessage
{
    /// <summary>任務 ID。</summary>
    public required string TaskId { get; init; }

    /// <summary>任務的說明。</summary>
    public required string Description { get; init; }

    /// <summary>到目前為止用掉的 token、工具次數與時間。</summary>
    public required TaskUsage Usage { get; init; }

    /// <summary>啟動這個任務的工具呼叫 ID。</summary>
    public string? ToolUseId { get; init; }

    /// <summary>最近一次用的工具。</summary>
    public string? LastToolName { get; init; }

    /// <summary>
    /// 一句話的進度摘要，例如「正在分析驗證模組」；開啟 <see cref="ClaudeAgentOptions.AgentProgressSummaries"/> 才有。
    /// Python SDK 沒有這個欄位，對應 TypeScript SDK 的 <c>summary</c>。
    /// </summary>
    public string? Summary { get; init; }
}
