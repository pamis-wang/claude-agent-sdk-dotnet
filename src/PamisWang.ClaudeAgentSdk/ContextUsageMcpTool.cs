using System.Text.Json;

namespace PamisWang.ClaudeAgentSdk;

/// <summary><see cref="ContextUsage.McpTools"/> 的一個工具。</summary>
public sealed record ContextUsageMcpTool
{
    /// <summary>工具名稱，例如 <c>mcp__linear__create_issue</c>。</summary>
    public required string Name { get; init; }

    /// <summary>提供這個工具的 MCP server。</summary>
    public required string ServerName { get; init; }

    /// <summary>工具定義佔的 token 數。</summary>
    public required int Tokens { get; init; }

    /// <summary>工具定義是否已經載入 context；CLI 沒回報時是 <see langword="null"/>。</summary>
    public bool? IsLoaded { get; init; }

    /// <summary>原始的內容。</summary>
    public JsonElement Raw { get; init; }
}
