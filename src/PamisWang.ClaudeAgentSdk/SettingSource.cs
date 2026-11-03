namespace PamisWang.ClaudeAgentSdk;

/// <summary>
/// 要載入的設定檔來源，用在 <see cref="ClaudeAgentOptions.SettingSources"/>。對應 Python SDK 的 <c>SettingSource</c>。
/// CLI 之後多了這裡還沒列的來源，可以用建構式直接帶 CLI 認得的字串。
/// </summary>
/// <param name="Value">CLI 參數裡的來源名稱，例如 <c>project</c>。</param>
public readonly record struct SettingSource(string Value)
{
    /// <summary>使用者的設定（<c>~/.claude/settings.json</c>）。</summary>
    public static SettingSource User { get; } = new("user");

    /// <summary>專案的設定（<c>.claude/settings.json</c>）。要載入 CLAUDE.md 也要包含這個。</summary>
    public static SettingSource Project { get; } = new("project");

    /// <summary>本機的設定（<c>.claude/settings.local.json</c>）。</summary>
    public static SettingSource Local { get; } = new("local");

    /// <summary>CLI 參數裡的來源名稱。</summary>
    /// <exception cref="ArgumentException">建立時給了空的名稱，或名稱含有逗號（多個來源是用逗號串起來傳給 CLI 的）。</exception>
    public string Value { get; } = string.IsNullOrWhiteSpace(Value) || Value.Contains(',')
        ? throw new ArgumentException("設定來源的名稱不能是空的，也不能含逗號。", nameof(Value))
        : Value;

    /// <summary>回傳 <see cref="Value"/>。</summary>
    public override string ToString() => Value;
}
