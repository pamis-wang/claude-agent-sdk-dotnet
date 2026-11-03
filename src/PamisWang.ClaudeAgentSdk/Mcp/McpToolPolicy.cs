namespace PamisWang.ClaudeAgentSdk.Mcp;

/// <summary>遠端 MCP server 裡某個工具的權限政策。Python SDK 沒有這個型別，對應 TypeScript SDK 的 <c>McpServerToolPolicy</c>。</summary>
/// <param name="Name">工具名稱，不含 <c>mcp__server__</c> 前綴。</param>
public sealed record McpToolPolicy(string Name)
{
    /// <summary>
    /// <c>always_allow</c>（一律允許）、<c>always_ask</c>（一律詢問）或 <c>always_deny</c>（一律拒絕）。
    /// 用字串而不是 enum，跟 CLI 的協定一樣。
    /// </summary>
    public string? PermissionPolicy { get; init; }
}
