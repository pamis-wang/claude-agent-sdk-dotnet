namespace PamisWang.ClaudeAgentSdk.Messages;

/// <summary>任務用掉的資源，見 <see cref="TaskProgressMessage.Usage"/> 與 <see cref="TaskNotificationMessage.Usage"/>。</summary>
/// <param name="TotalTokens">token 總數。</param>
/// <param name="ToolUses">工具的使用次數。</param>
/// <param name="DurationMilliseconds">花的時間（毫秒）。</param>
public sealed record TaskUsage(int TotalTokens, int ToolUses, long DurationMilliseconds);
