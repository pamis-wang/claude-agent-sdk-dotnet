using System.Text.Json;

namespace PamisWang.ClaudeAgentSdk;

/// <summary><see cref="ContextUsage.Agents"/> 的一個 subagent 定義。</summary>
public sealed record ContextUsageAgent
{
    /// <summary>subagent 的類型名稱。</summary>
    public required string AgentType { get; init; }

    /// <summary>定義的來源，例如 <c>projectSettings</c>、<c>userSettings</c>、<c>plugin</c>。</summary>
    public required string Source { get; init; }

    /// <summary>定義佔的 token 數。</summary>
    public required int Tokens { get; init; }

    /// <summary>原始的內容。</summary>
    public JsonElement Raw { get; init; }
}
