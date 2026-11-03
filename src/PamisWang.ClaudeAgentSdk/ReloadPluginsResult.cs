using System.Text.Json;
using PamisWang.ClaudeAgentSdk.Mcp;

namespace PamisWang.ClaudeAgentSdk;

/// <summary>
/// <see cref="ClaudeAgentClient.ReloadPluginsAsync"/> 的結果：重新載入之後這個 session 的指令、subagent、外掛與 MCP server。
/// Python SDK 沒有這個方法，對應 TypeScript SDK 的 <c>SDKControlReloadPluginsResponse</c>。
/// </summary>
public sealed record ReloadPluginsResult
{
    /// <summary>可以用的斜線指令與 skill。</summary>
    public required IReadOnlyList<SlashCommand> Commands { get; init; }

    /// <summary>可以交辦的 subagent，原始的 JSON（<c>name</c>、<c>description</c>、<c>model</c>）。</summary>
    public required IReadOnlyList<JsonElement> Agents { get; init; }

    /// <summary>載入的外掛。</summary>
    public required IReadOnlyList<PluginInfo> Plugins { get; init; }

    /// <summary>MCP server 的連線狀態。</summary>
    public required IReadOnlyList<McpServerStatus> McpServers { get; init; }

    /// <summary>載入時發生的錯誤數。</summary>
    public required int ErrorCount { get; init; }

    /// <summary>
    /// 要求 <c>holdOnCacheImpact</c> 時才有：<see langword="true"/> 表示沒有套用（會改變工具清單、讓提示詞快取失效），
    /// 上面的清單是目前的樣子，會改變什麼見 <see cref="Raw"/> 的 <c>cache_impact</c>。
    /// </summary>
    public bool? Held { get; init; }

    /// <summary>CLI 回應的原始內容。</summary>
    public JsonElement Raw { get; init; }
}
