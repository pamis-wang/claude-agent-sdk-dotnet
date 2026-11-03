using System.Text.Json;

namespace PamisWang.ClaudeAgentSdk.Mcp;

/// <summary>
/// 外部 MCP server 的設定：<see cref="McpStdioServerConfig"/>、<see cref="McpSseServerConfig"/> 或 <see cref="McpHttpServerConfig"/>。
/// 用在 <see cref="ClaudeAgentOptions.McpServers"/> 與 <see cref="ClaudeAgentClient.SetMcpServersAsync"/>。
/// 對應 Python SDK 的 <c>McpServerConfig</c>；SDK 內的 MCP server（<c>type: sdk</c>）這裡還沒有。
/// </summary>
public abstract record McpServerConfig
{
    private protected McpServerConfig()
    {
    }

    /// <summary>每次請求與工具呼叫的逾時（毫秒）。沒指定就用 Claude Code 的預設。Python SDK 沒有這個欄位，對應 TypeScript SDK 的 <c>timeout</c>。</summary>
    public int? Timeout { get; init; }

    /// <summary>
    /// <see langword="true"/>：這個 server 的工具一律放進提示詞，不延後到工具搜尋才載入；開對話時也會等它連上（最多 5 秒）。
    /// <see langword="false"/>：一律延後。沒指定時，開啟工具搜尋就延後。Python SDK 沒有這個欄位，對應 TypeScript SDK 的 <c>alwaysLoad</c>。
    /// </summary>
    public bool? AlwaysLoad { get; init; }

    /// <summary>寫成 CLI 的 MCP 設定格式。</summary>
    internal void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        WriteFields(writer);
        if (Timeout is { } timeout)
        {
            writer.WriteNumber("timeout", timeout);
        }

        if (AlwaysLoad is { } alwaysLoad)
        {
            writer.WriteBoolean("alwaysLoad", alwaysLoad);
        }

        writer.WriteEndObject();
    }

    /// <summary>寫出各種 server 自己的欄位，含 <c>type</c>。</summary>
    private protected abstract void WriteFields(Utf8JsonWriter writer);

    /// <summary>寫出 key 是字串、value 也是字串的物件，例如環境變數與 HTTP 標頭；空的就不寫。</summary>
    private protected static void WriteStringMap(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        writer.WriteStartObject(name);
        foreach (var (key, value) in values)
        {
            writer.WriteString(key, value);
        }

        writer.WriteEndObject();
    }

    /// <summary>寫出每個工具的權限政策；空的就不寫。</summary>
    private protected static void WriteToolPolicies(Utf8JsonWriter writer, IReadOnlyList<McpToolPolicy> tools)
    {
        if (tools.Count == 0)
        {
            return;
        }

        writer.WriteStartArray("tools");
        foreach (var tool in tools)
        {
            writer.WriteStartObject();
            writer.WriteString("name", tool.Name);
            if (tool.PermissionPolicy is not null)
            {
                writer.WriteString("permission_policy", tool.PermissionPolicy);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}
