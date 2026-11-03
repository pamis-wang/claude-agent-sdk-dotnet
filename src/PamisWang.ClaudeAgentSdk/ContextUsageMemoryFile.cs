using System.Text.Json;

namespace PamisWang.ClaudeAgentSdk;

/// <summary><see cref="ContextUsage.MemoryFiles"/> 的一個檔案。</summary>
public sealed record ContextUsageMemoryFile
{
    /// <summary>檔案路徑。</summary>
    public required string Path { get; init; }

    /// <summary>來源的顯示名稱，例如 <c>Project</c>、<c>User</c>。</summary>
    public required string Type { get; init; }

    /// <summary>這個檔案佔的 token 數。</summary>
    public required int Tokens { get; init; }

    /// <summary>原始的內容。</summary>
    public JsonElement Raw { get; init; }
}
