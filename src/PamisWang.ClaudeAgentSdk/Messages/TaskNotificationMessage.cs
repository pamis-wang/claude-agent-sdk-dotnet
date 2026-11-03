namespace PamisWang.ClaudeAgentSdk.Messages;

/// <summary>
/// 任務結束了（<c>system</c>／<c>task_notification</c>）：完成、失敗或被停止。對應 Python SDK 的 <c>TaskNotificationMessage</c>。
/// 不是每個任務結束時都會送這則；背景任務可能只送 <see cref="TaskUpdatedMessage"/>，要追蹤哪些任務還在跑，兩種都要看。
/// </summary>
public sealed record TaskNotificationMessage : SystemMessage
{
    /// <summary>任務 ID。</summary>
    public required string TaskId { get; init; }

    /// <summary><c>completed</c>、<c>failed</c> 或 <c>stopped</c>。用字串而不是 enum，CLI 加了新的狀態也不會解析失敗。</summary>
    public required string Status { get; init; }

    /// <summary>任務輸出寫在哪個檔案。</summary>
    public required string OutputFile { get; init; }

    /// <summary>結果的摘要。</summary>
    public required string Summary { get; init; }

    /// <summary>啟動這個任務的工具呼叫 ID。</summary>
    public string? ToolUseId { get; init; }

    /// <summary>總共用掉的 token、工具次數與時間。</summary>
    public TaskUsage? Usage { get; init; }
}
