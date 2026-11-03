namespace PamisWang.ClaudeAgentSdk.Permissions;

/// <summary>
/// 權限模式：工具執行前要不要問、由誰決定。對應 Python SDK 的 <c>PermissionMode</c>。
/// 用 <see cref="Default"/>、<see cref="Auto"/> 這些屬性取得；CLI 之後多了這裡還沒列的模式，可以用建構式直接帶 CLI 認得的字串。
/// 跟 PamisWang.CodexAgentSdk 的字串值型別一樣，用值比較。
/// </summary>
/// <remarks>
/// 開對話時用 <see cref="ClaudeAgentOptions.PermissionMode"/> 指定，對話中途用 <see cref="ClaudeAgentClient.SetPermissionModeAsync"/> 換。
/// </remarks>
/// <param name="Value">CLI 協定裡的模式名稱，例如 <c>acceptEdits</c>。</param>
public readonly record struct PermissionMode(string Value)
{
    /// <summary>
    /// 一般模式（<c>default</c>，Claude Code 介面上叫 Manual）：照設定檔的規則，沒有事先允許的動作會詢問，
    /// 由 <see cref="ClaudeAgentOptions.CanUseTool"/> 回答。CLI 也接受 <c>manual</c>，當成這個模式。
    /// </summary>
    public static PermissionMode Default { get; } = new("default");

    /// <summary>自動接受改檔（<c>acceptEdits</c>）：工作目錄裡的檔案編輯直接通過，其他照 <see cref="Default"/>。</summary>
    public static PermissionMode AcceptEdits { get; } = new("acceptEdits");

    /// <summary>規劃模式（<c>plan</c>）：只讀和規劃，不執行會改動東西的工具。</summary>
    public static PermissionMode Plan { get; } = new("plan");

    /// <summary>
    /// 略過所有權限檢查（<c>bypassPermissions</c>）。只適合沒有網路的沙箱。
    /// 對話中途要切到這個模式，開對話時要設定 <see cref="ClaudeAgentOptions.AllowDangerouslySkipPermissions"/>。
    /// </summary>
    public static PermissionMode BypassPermissions { get; } = new("bypassPermissions");

    /// <summary>不詢問（<c>dontAsk</c>）：沒有事先允許的動作一律拒絕，<see cref="ClaudeAgentOptions.CanUseTool"/> 不會被呼叫。</summary>
    public static PermissionMode DontAsk { get; } = new("dontAsk");

    /// <summary>
    /// 自動模式（<c>auto</c>）：由模型分類器判斷每個動作要允許還是拒絕，判斷不了才經 <see cref="ClaudeAgentOptions.CanUseTool"/> 詢問。
    /// 帳號不能用自動模式時，CLI 會退回 <see cref="Default"/>。
    /// </summary>
    public static PermissionMode Auto { get; } = new("auto");

    /// <summary>CLI 協定裡的模式名稱。</summary>
    /// <exception cref="ArgumentException">建立時給了空的名稱。</exception>
    public string Value { get; } = string.IsNullOrWhiteSpace(Value)
        ? throw new ArgumentException("權限模式的名稱不能是空的。", nameof(Value))
        : Value;

    /// <summary>回傳 <see cref="Value"/>。</summary>
    public override string ToString() => Value;
}
