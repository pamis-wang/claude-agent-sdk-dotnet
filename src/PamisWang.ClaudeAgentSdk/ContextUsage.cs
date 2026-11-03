using System.Text.Json;

namespace PamisWang.ClaudeAgentSdk;

/// <summary>
/// 目前 context window 的用量，依類別拆開，跟 CLI 的 <c>/context</c> 指令顯示的一樣。
/// 由 <see cref="ClaudeAgentClient.GetContextUsageAsync"/> 取得，對應 Python SDK 的 <c>ContextUsageResponse</c>。
/// 這裡只放常用的欄位；<c>messageBreakdown</c>、<c>skills</c>、<c>slashCommands</c>、<c>apiUsage</c>、畫方格圖用的 <c>gridRows</c> 等其他欄位從 <see cref="Raw"/> 讀。
/// </summary>
public sealed record ContextUsage
{
    /// <summary>計算用量的模型，例如 <c>claude-haiku-5-5</c>。</summary>
    public required string Model { get; init; }

    /// <summary>目前 context 裡的 token 總數。</summary>
    public required int TotalTokens { get; init; }

    /// <summary>實際可用的上限（token 數），可能因為保留給自動壓縮的空間而比 <see cref="RawMaxTokens"/> 小。</summary>
    public required int MaxTokens { get; init; }

    /// <summary>模型原本的 context window 大小（token 數）。</summary>
    public required int RawMaxTokens { get; init; }

    /// <summary>已用的百分比，0 到 100。CLI 2.1.309 送的是四捨五入後的整數。</summary>
    public required double Percentage { get; init; }

    /// <summary>這個 session 是否開啟自動壓縮。</summary>
    public required bool IsAutoCompactEnabled { get; init; }

    /// <summary>自動壓縮的門檻（token 數）。CLI 沒回報時是 <see langword="null"/>。</summary>
    public int? AutoCompactThreshold { get; init; }

    /// <summary>
    /// 依類別的用量，包含剩下的空間（<see cref="ContextUsageCategory.Kind"/> 是 <c>free</c>）與保留給壓縮的空間（<c>buffer</c>）。
    /// 延後載入的工具（<c>deferred</c>）只是列出來，不算在用量裡。
    /// </summary>
    public required IReadOnlyList<ContextUsageCategory> Categories { get; init; }

    /// <summary>載入的 CLAUDE.md 與 memory 檔。</summary>
    public IReadOnlyList<ContextUsageMemoryFile> MemoryFiles { get; init; } = [];

    /// <summary>MCP server 提供的工具。</summary>
    public IReadOnlyList<ContextUsageMcpTool> McpTools { get; init; } = [];

    /// <summary>自訂的 subagent 定義；內建的不列。</summary>
    public IReadOnlyList<ContextUsageAgent> Agents { get; init; } = [];

    /// <summary>CLI 回應的原始內容。</summary>
    public JsonElement Raw { get; init; }
}
