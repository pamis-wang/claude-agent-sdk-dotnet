using System.Text.Json;

namespace PamisWang.ClaudeAgentSdk.Mcp;

/// <summary>
/// <see cref="ClaudeAgentClient.ReadMcpResourceAsync"/> 讀到的一項內容。內容來自第三方的 MCP server（通常是 HTML），顯示時要放在沙箱裡。
/// </summary>
public sealed record McpResourceContent
{
    /// <summary>資源的 URI。</summary>
    public required string Uri { get; init; }

    /// <summary>內容的 MIME 類型。</summary>
    public string? MimeType { get; init; }

    /// <summary>文字內容。</summary>
    public string? Text { get; init; }

    /// <summary>二進位內容，Base64 編碼。</summary>
    public string? Blob { get; init; }

    /// <summary>原始的內容，含 server 送的 <c>_meta</c>（MCP Apps 的 <c>ui.csp</c> 等）。</summary>
    public JsonElement Raw { get; init; }
}
