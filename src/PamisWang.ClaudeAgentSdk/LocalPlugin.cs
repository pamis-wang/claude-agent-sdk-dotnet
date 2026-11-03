namespace PamisWang.ClaudeAgentSdk;

/// <summary>
/// 這個 session 要載入的本機外掛，用在 <see cref="ClaudeAgentOptions.Plugins"/>。對應 Python SDK 的 <c>SdkPluginConfig</c>（<c>type: local</c>，目前只有這一種）。
/// 外掛可以提供指令、subagent、skill 與 hook。
/// </summary>
/// <param name="Path">外掛的資料夾或 <c>.zip</c>；資料夾裡如果是多個外掛，每個都會載入。</param>
public sealed record LocalPlugin(string Path)
{
    /// <summary>
    /// 只載入外掛的 skill、hook、subagent 與指令，不讀它的 <c>.mcp.json</c> 與 manifest 裡的 MCP server（<c>--plugin-dir-no-mcp</c>）。
    /// 外掛的 MCP 連線由應用程式自己管理時用。Python SDK 沒有這個欄位，對應 TypeScript SDK 的 <c>skipMcpDiscovery</c>。
    /// </summary>
    public bool SkipMcpDiscovery { get; init; }
}
