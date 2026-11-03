namespace PamisWang.ClaudeAgentSdk.Mcp;

/// <summary>MCP server 在連線時回報的名稱與版本，見 <see cref="McpServerStatus.ServerInfo"/>。</summary>
/// <param name="Name">server 回報的名稱。</param>
/// <param name="Version">server 回報的版本。</param>
public sealed record McpServerInfo(string Name, string Version);
