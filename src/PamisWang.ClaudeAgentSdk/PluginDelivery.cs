namespace PamisWang.ClaudeAgentSdk;

/// <summary>
/// <see cref="ClaudeAgentOptions.Plugins"/> 怎麼交給 CLI。Python SDK 沒有這個選項，對應 TypeScript SDK 的 <c>pluginDelivery</c>。
/// </summary>
public enum PluginDelivery
{
    /// <summary>每個外掛一個 <c>--plugin-dir</c> 參數。任何版本的 CLI 都能用，但外掛很多時命令列會很長，Windows 的命令列上限是 32,767 個字元。</summary>
    CommandLine,

    /// <summary>
    /// 放在初始化握手裡送，CLI 用 <c>--await-initialize</c> 啟動，命令列長度跟外掛數量無關，載入的結果一樣。
    /// 需要 Claude Code 2.1.261 以後的版本，舊版會因為不認得參數而無法啟動。
    /// </summary>
    Initialize,
}
