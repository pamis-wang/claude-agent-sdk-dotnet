using System.Text.Json;

namespace PamisWang.ClaudeAgentSdk.Messages;

/// <summary><see cref="TaskUpdatedMessage"/> 改變的欄位；沒有改變的是 <see langword="null"/>。</summary>
public sealed record TaskUpdatePatch
{
    /// <summary>
    /// <c>pending</c>、<c>running</c>、<c>paused</c>，或結束的 <c>completed</c>、<c>failed</c>、<c>killed</c>。
    /// 用字串而不是 enum，CLI 加了新的狀態也不會解析失敗。
    /// </summary>
    public string? Status { get; init; }

    /// <summary>新的說明。</summary>
    public string? Description { get; init; }

    /// <summary>結束的時間（Unix 毫秒）。</summary>
    public long? EndTime { get; init; }

    /// <summary>暫停的總時間（毫秒）。</summary>
    public long? TotalPausedMilliseconds { get; init; }

    /// <summary>失敗的原因。</summary>
    public string? Error { get; init; }

    /// <summary>是不是移到背景了。</summary>
    public bool? IsBackgrounded { get; init; }

    /// <summary>
    /// <see cref="Status"/> 是結束的狀態：<c>completed</c>、<c>failed</c>、<c>stopped</c> 或 <c>killed</c>。
    /// 跟 Python SDK 的 <c>TERMINAL_TASK_STATUSES</c> 一樣；<see cref="TaskNotificationMessage"/> 的 <c>stopped</c> 跟這裡的 <c>killed</c> 是同一回事。
    /// </summary>
    public bool IsTerminal => Status is "completed" or "failed" or "stopped" or "killed";

    /// <summary>原始的內容。</summary>
    public JsonElement Raw { get; init; }
}
