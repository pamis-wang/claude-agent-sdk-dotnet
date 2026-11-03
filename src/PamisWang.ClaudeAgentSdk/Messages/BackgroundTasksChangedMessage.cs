namespace PamisWang.ClaudeAgentSdk.Messages;

/// <summary>
/// 背景任務的清單變了（<c>system</c>／<c>background_tasks_changed</c>），<see cref="Tasks"/> 是目前所有在背景的任務。
/// subagent 剛開始時在前景、之後才移到背景，所以這份清單不一定包含所有還在跑的任務；要追蹤任務的開始與結束，看 <see cref="TaskStartedMessage"/> 那幾種訊息。
/// Python SDK 沒有這個型別，對應 TypeScript SDK 的 <c>SDKBackgroundTasksChangedMessage</c>。
/// </summary>
public sealed record BackgroundTasksChangedMessage : SystemMessage
{
    /// <summary>目前在背景的任務。</summary>
    public required IReadOnlyList<BackgroundTask> Tasks { get; init; }
}
