using System.Text.Json;

namespace PamisWang.ClaudeAgentSdk.Mcp;

/// <summary>
/// <see cref="ClaudeAgentClient.SetMcpServersAsync"/> 的結果。Python SDK 沒有這個方法，對應 TypeScript SDK 的 <c>McpSetServersResult</c>。
/// </summary>
public sealed record McpSetServersResult
{
    /// <summary>新加入的 server 名稱。</summary>
    public required IReadOnlyList<string> Added { get; init; }

    /// <summary>移除的 server 名稱。</summary>
    public required IReadOnlyList<string> Removed { get; init; }

    /// <summary>設定失敗的 server，key 是名稱，value 是原因。</summary>
    public required IReadOnlyDictionary<string, string> Errors { get; init; }

    /// <summary>CLI 回應的原始內容。</summary>
    public JsonElement Raw { get; init; }
}
