namespace PamisWang.ClaudeAgentSdk;

/// <summary>一個載入的外掛，見 <see cref="ReloadPluginsResult.Plugins"/>。</summary>
public sealed record PluginInfo
{
    /// <summary>外掛名稱。</summary>
    public required string Name { get; init; }

    /// <summary>外掛的位置。</summary>
    public required string Path { get; init; }

    /// <summary>外掛的來源，例如 marketplace 名稱。</summary>
    public string? Source { get; init; }

    /// <summary>外掛的版本。</summary>
    public string? Version { get; init; }
}
