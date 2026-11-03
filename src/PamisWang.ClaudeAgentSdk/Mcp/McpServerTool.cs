using System.Text.Json;

namespace PamisWang.ClaudeAgentSdk.Mcp;

/// <summary>MCP server 提供的一個工具，見 <see cref="McpServerStatus.Tools"/>。</summary>
public sealed record McpServerTool
{
    /// <summary>工具名稱，不含 <c>mcp__server__</c> 前綴。</summary>
    public required string Name { get; init; }

    /// <summary>工具的說明。</summary>
    public string? Description { get; init; }

    /// <summary>server 標示這個工具只讀、不改動東西（<c>annotations.readOnly</c>）。</summary>
    public bool? IsReadOnly { get; init; }

    /// <summary>server 標示這個工具可能造成破壞性的改動（<c>annotations.destructive</c>）。</summary>
    public bool? IsDestructive { get; init; }

    /// <summary>server 標示這個工具會接觸外部世界（<c>annotations.openWorld</c>）。</summary>
    public bool? IsOpenWorld { get; init; }

    /// <summary>原始的內容，含 <c>_meta</c>。</summary>
    public JsonElement Raw { get; init; }
}
