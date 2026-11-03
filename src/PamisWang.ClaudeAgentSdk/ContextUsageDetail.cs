namespace PamisWang.ClaudeAgentSdk;

/// <summary>
/// <see cref="ClaudeAgentClient.GetContextUsageAsync"/> 要算得多細（<c>detail</c>）。
/// Python SDK 沒有這個參數，對應 TypeScript SDK <c>getContextUsage()</c> 的 <c>detail</c>。
/// </summary>
public enum ContextUsageDetail
{
    /// <summary>每個類別都用 token 計算 API 算，數字較準，但要多等一下（實測約 0.7 秒）。不指定時 CLI 就用這個。</summary>
    Full,

    /// <summary>用上一次回應的用量加上本機估算，不呼叫 token 計算 API；很快，但各類別的數字是估計值。</summary>
    Summary,
}
