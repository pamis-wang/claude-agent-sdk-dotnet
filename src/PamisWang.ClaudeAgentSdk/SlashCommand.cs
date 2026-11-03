using System.Text.Json;

namespace PamisWang.ClaudeAgentSdk;

/// <summary>一個斜線指令或 skill，見 <see cref="ReloadPluginsResult.Commands"/> 與 <see cref="ClaudeAgentClient.ReloadSkillsAsync"/>。</summary>
public sealed record SlashCommand
{
    /// <summary>名稱，不含開頭的 <c>/</c>。</summary>
    public required string Name { get; init; }

    /// <summary>說明。</summary>
    public required string Description { get; init; }

    /// <summary>參數的提示，例如 <c>[file]</c>；沒有參數時是空字串。</summary>
    public string ArgumentHint { get; init; } = "";

    /// <summary>原始的內容，含 <c>aliases</c> 等其他欄位。</summary>
    public JsonElement Raw { get; init; }
}
