using System.Collections.ObjectModel;
using System.Text.Json;

namespace PamisWang.ClaudeAgentSdk.Mcp;

/// <summary>
/// 用 Streamable HTTP 連線的遠端 MCP server。對應 Python SDK 的 <c>McpHttpServerConfig</c>。
/// ASP.NET Core 應用程式可以在自己的程序裡開 MCP 端點，再用這個設定交給 Claude Code，工具就能直接用應用程式的狀態。
/// </summary>
/// <param name="Url">server 的網址。</param>
public sealed record McpHttpServerConfig(string Url) : McpServerConfig
{
    /// <summary>每個請求都帶上的 HTTP 標頭，例如驗證用的 token。</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = ReadOnlyDictionary<string, string>.Empty;

    /// <summary>個別工具的權限政策。Python SDK 沒有這個欄位，對應 TypeScript SDK 的 <c>tools</c>。</summary>
    public IReadOnlyList<McpToolPolicy> Tools { get; init; } = [];

    /// <inheritdoc />
    private protected override void WriteFields(Utf8JsonWriter writer)
    {
        writer.WriteString("type", "http");
        writer.WriteString("url", Url);
        WriteStringMap(writer, "headers", Headers);
        WriteToolPolicies(writer, Tools);
    }
}
