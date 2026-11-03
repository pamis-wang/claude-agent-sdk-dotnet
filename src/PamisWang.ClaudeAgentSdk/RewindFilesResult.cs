using System.Text.Json;

namespace PamisWang.ClaudeAgentSdk;

/// <summary>
/// <see cref="ClaudeAgentClient.RewindFilesAsync"/> 的結果。Python SDK 不回傳結果，對應 TypeScript SDK 的 <c>RewindFilesResult</c>。
/// </summary>
public sealed record RewindFilesResult
{
    /// <summary>能不能還原（預覽時）或是否已經還原。<see langword="false"/> 時原因在 <see cref="Error"/>。</summary>
    public required bool CanRewind { get; init; }

    /// <summary>不能還原的原因，例如沒有開啟 <see cref="ClaudeAgentOptions.EnableFileCheckpointing"/>。</summary>
    public string? Error { get; init; }

    /// <summary>會變動（或已經變動）的檔案。</summary>
    public IReadOnlyList<string> FilesChanged { get; init; } = [];

    /// <summary>增加的行數。</summary>
    public int? Insertions { get; init; }

    /// <summary>刪除的行數。</summary>
    public int? Deletions { get; init; }

    /// <summary>
    /// 因為是符號連結、硬連結或其他不是一般檔案的東西，沒有還原的檔案數。只有真正還原時才有，預覽時不會設定。
    /// </summary>
    public int? SkippedLinks { get; init; }

    /// <summary>CLI 回應的原始內容。</summary>
    public JsonElement Raw { get; init; }
}
