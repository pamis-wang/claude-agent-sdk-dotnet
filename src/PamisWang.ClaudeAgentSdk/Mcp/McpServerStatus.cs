using System.Text.Json;

namespace PamisWang.ClaudeAgentSdk.Mcp;

/// <summary>一個 MCP server 的連線狀態。由 <see cref="ClaudeAgentClient.GetMcpStatusAsync"/> 取得，對應 Python SDK 的 <c>McpServerStatus</c>。</summary>
public sealed record McpServerStatus
{
    /// <summary>server 名稱。</summary>
    public required string Name { get; init; }

    /// <summary>
    /// <c>connected</c>（已連線）、<c>pending</c>（連線中）、<c>failed</c>（失敗，原因在 <see cref="Error"/>）、
    /// <c>needs-auth</c>（要登入）或 <c>disabled</c>（已關閉）。用字串而不是 enum，CLI 加了新的狀態也不會解析失敗。
    /// </summary>
    public required string Status { get; init; }

    /// <summary>server 自己回報的名稱與版本；連上之後才有。</summary>
    public McpServerInfo? ServerInfo { get; init; }

    /// <summary>連線失敗的原因。</summary>
    public string? Error { get; init; }

    /// <summary>設定來自哪個範圍，例如 <c>project</c>、<c>user</c>、<c>local</c>。</summary>
    public string? Scope { get; init; }

    /// <summary>設定的來源，例如外掛的名稱。</summary>
    public string? Source { get; init; }

    /// <summary>server 提供的工具；連上之後才有。</summary>
    public IReadOnlyList<McpServerTool> Tools { get; init; } = [];

    /// <summary>server 的設定（stdio、sse、http 等），原始的 JSON。</summary>
    public JsonElement? Config { get; init; }

    /// <summary>CLI 回應的原始內容。</summary>
    public JsonElement Raw { get; init; }
}
