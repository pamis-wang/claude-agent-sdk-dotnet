using System.Text.Json;

namespace PamisWang.ClaudeAgentSdk;

/// <summary>
/// <see cref="ContextUsage.Categories"/> 的一列，例如系統提示詞、工具、對話訊息。
/// 要找特定的一列用 <see cref="Id"/>，要分類用 <see cref="Kind"/>；不要用 <see cref="Name"/>，那是顯示用的英文名稱。
/// </summary>
public sealed record ContextUsageCategory
{
    /// <summary>
    /// 穩定的識別碼：<c>system_prompt</c>、<c>system_tools</c>、<c>system_tools_deferred</c>、<c>mcp_tools</c>、<c>mcp_tools_deferred</c>、
    /// <c>mcp_instructions</c>、<c>custom_agents</c>、<c>memory_files</c>、<c>skills</c>、<c>messages</c>、
    /// <c>autocompact_buffer</c>、<c>compact_buffer</c>、<c>free_space</c>。
    /// 之後的 CLI 可能多出新的值；舊版 CLI 沒有這個欄位時是 <see langword="null"/>。
    /// </summary>
    public string? Id { get; init; }

    /// <summary>CLI 顯示的名稱，例如 <c>Messages</c>、<c>MCP tools (deferred)</c>。</summary>
    public required string Name { get; init; }

    /// <summary>這一列的 token 數，可能是 0。</summary>
    public required int Tokens { get; init; }

    /// <summary>
    /// 這一列是什麼：<c>used</c> 是佔用 context 的內容、<c>free</c> 是剩下的空間、<c>buffer</c> 是保留給壓縮的空間、
    /// <c>deferred</c> 是延後載入、不在 context 裡的工具定義（不算在用量裡）。
    /// Python SDK 的型別沒有這個欄位；舊版 CLI 沒有時是 <see langword="null"/>。
    /// </summary>
    public string? Kind { get; init; }

    /// <summary>是否為延後載入、不在 context 裡的工具定義。</summary>
    public bool IsDeferred { get; init; }

    /// <summary>原始的這一列，包含 CLI 畫面用的 <c>color</c>。</summary>
    public JsonElement Raw { get; init; }
}
