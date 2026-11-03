using System.Text.Json;

namespace PamisWang.ClaudeAgentSdk.Messages;

/// <summary>
/// 一輪結束（<c>type: result</c>）：成功、出錯或被中斷，附上耗時、費用與用量。
/// 屬性對應 Python SDK <c>ResultMessage</c> 的同名欄位；名稱不同的，在屬性的說明註明。
/// </summary>
public sealed record ResultMessage : ClaudeMessage
{
    /// <summary>
    /// <c>success</c>，或 <c>error_during_execution</c>、<c>error_max_turns</c>、<c>error_max_budget_usd</c>、
    /// <c>error_max_structured_output_retries</c> 這類錯誤。
    /// </summary>
    public required string Subtype { get; init; }

    /// <summary>
    /// 這一輪是否以錯誤結束，例如被中斷。API 錯誤（例如沒登入）時 <see cref="Subtype"/> 仍是 <c>success</c>，只有這裡是 <see langword="true"/>。
    /// </summary>
    public required bool IsError { get; init; }

    /// <summary>這一輪花的時間（毫秒）。對應 Python 的 <c>duration_ms</c>。</summary>
    public required long DurationMilliseconds { get; init; }

    /// <summary>其中等 API 回應的時間（毫秒）。對應 Python 的 <c>duration_api_ms</c>。</summary>
    public required long ApiDurationMilliseconds { get; init; }

    /// <summary>這一輪來回呼叫模型的次數。對應 Python 的 <c>num_turns</c>。</summary>
    public required int TurnCount { get; init; }

    /// <summary>
    /// 模型最後的回覆文字。<see cref="Subtype"/> 是 <c>error_</c> 開頭的錯誤時是 <see langword="null"/>，原因見 <see cref="Errors"/>；
    /// API 錯誤（例如沒登入）時是錯誤訊息，錯誤的種類見那一輪 <see cref="AssistantMessage.Error"/>。
    /// </summary>
    public string? Result { get; init; }

    /// <summary>出錯的說明。</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>這個 session 到目前為止累計的估計費用（美元），不是只有這一輪。</summary>
    public double? TotalCostUsd { get; init; }

    /// <summary>模型停下來的原因。</summary>
    public string? StopReason { get; init; }

    /// <summary>這一輪怎麼結束的，例如 <c>completed</c>、<c>aborted_streaming</c>、<c>aborted_tools</c>。</summary>
    public string? TerminalReason { get; init; }

    /// <summary>被拒絕的工具呼叫。</summary>
    public IReadOnlyList<PermissionDenial> PermissionDenials { get; init; } = [];

    /// <summary>API 錯誤的 HTTP 狀態碼。</summary>
    public int? ApiErrorStatus { get; init; }

    /// <summary>token 用量，原樣保留。</summary>
    public JsonElement? Usage { get; init; }

    /// <summary>各模型的用量與費用，原樣保留。</summary>
    public JsonElement? ModelUsage { get; init; }

    /// <summary>
    /// 指定 <see cref="ClaudeAgentOptions.OutputSchema"/> 時，符合 schema 的結果；可以用 <c>Deserialize&lt;T&gt;()</c> 轉成自己的型別。
    /// 模型一直產生不符合 schema 的輸出時，<see cref="Subtype"/> 是 <c>error_max_structured_output_retries</c>。
    /// </summary>
    public JsonElement? StructuredOutput { get; init; }
}
