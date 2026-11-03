namespace PamisWang.ClaudeAgentSdk.Messages;

/// <summary>
/// 背景任務的狀態變了（<c>system</c>／<c>task_updated</c>），<see cref="Patch"/> 只帶改變的欄位。對應 Python SDK 的 <c>TaskUpdatedMessage</c>。
/// 背景任務結束時可能只送這則、不送 <see cref="TaskNotificationMessage"/>，例如被停止的任務在這裡回報 <c>killed</c>；
/// 用 <see cref="TaskUpdatePatch.IsTerminal"/> 判斷任務是不是結束了。
/// </summary>
public sealed record TaskUpdatedMessage : SystemMessage
{
    /// <summary>任務 ID。</summary>
    public required string TaskId { get; init; }

    /// <summary>改變的欄位。</summary>
    public required TaskUpdatePatch Patch { get; init; }
}
