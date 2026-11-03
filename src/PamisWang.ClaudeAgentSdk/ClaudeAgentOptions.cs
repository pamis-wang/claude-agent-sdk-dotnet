using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using PamisWang.ClaudeAgentSdk.Permissions;

namespace PamisWang.ClaudeAgentSdk;

/// <summary>
/// 啟動 Claude Code 的選項。對應 Python SDK 的 <c>ClaudeAgentOptions</c>，目前只有第一版要用到的部分。
/// 屬性對應 Python 的同名欄位（例如 <c>CliPath</c> 是 <c>cli_path</c>）；名稱不同的，在屬性的說明註明。
/// </summary>
public sealed class ClaudeAgentOptions
{
    /// <summary>
    /// <c>claude</c> 執行檔的路徑。沒指定就從 PATH 與常見的安裝位置找。
    /// Windows 只執行原生的 <c>claude.exe</c>，不執行 npm 的 <c>claude.cmd</c> 這類批次檔。
    /// </summary>
    public string? CliPath { get; init; }

    /// <summary>
    /// 工作目錄，也決定 session 存在 <c>~/.claude/projects</c> 底下的哪個資料夾。沒指定就沿用目前程序的。
    /// 對應 Python 的 <c>cwd</c>。
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// 系統提示詞。沒指定時跟 Python SDK 一樣傳空的系統提示詞，不用 Claude Code 預設的；
    /// 要跟在終端機用 Claude Code 時一樣，指定 <see cref="SystemPromptPreset"/>。
    /// </summary>
    public SystemPrompt? SystemPrompt { get; init; }

    /// <summary>
    /// 模型，例如 <c>sonnet</c>、<c>haiku</c> 或完整的模型 ID。沒指定就用 Claude Code 的預設。
    /// 連線後可以用 <see cref="ClaudeAgentClient.SetModelAsync"/> 換。
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// 思考的深度（<c>--effort</c>）：<c>low</c>、<c>medium</c>、<c>high</c>、<c>xhigh</c> 或 <c>max</c>。
    /// 各模型支援哪些，見 <see cref="ClaudeAgentClient.ServerInfo"/> 裡 <c>models</c> 的 <c>supportedEffortLevels</c>。沒指定就用模型的預設。
    /// 連線後可以用 <see cref="ClaudeAgentClient.ApplyFlagSettingsAsync"/> 的 <c>effortLevel</c> 換。
    /// </summary>
    public string? Effort { get; init; }

    /// <summary>要接續的 session ID（<c>--resume</c>）。對應 Python 的 <c>resume</c>。</summary>
    public string? ResumeSessionId { get; init; }

    /// <summary>是否接收部分訊息（<c>--include-partial-messages</c>）：模型輸出時逐段送來的 <c>stream_event</c>。</summary>
    public bool IncludePartialMessages { get; init; }

    /// <summary>停用的工具名稱（<c>--disallowedTools</c>）。</summary>
    public IReadOnlyList<string> DisallowedTools { get; init; } = [];

    /// <summary>
    /// 模型來回次數的上限（<c>--max-turns</c>），每次送出訊息各自計算，不是整個 session 累計。
    /// 超過就停下來，<see cref="Messages.ResultMessage.Subtype"/> 是 <c>error_max_turns</c>。沒指定就不限制。
    /// 必須大於 0，否則 <see cref="ClaudeAgentClient.ConnectAsync"/> 丟出 <see cref="ArgumentOutOfRangeException"/>。
    /// </summary>
    public int? MaxTurns { get; init; }

    /// <summary>
    /// API 費用的上限，單位美元（<c>--max-budget-usd</c>），跟 <see cref="Messages.ResultMessage.TotalCostUsd"/> 一樣是整個 session 累計。
    /// 每次呼叫完 API 才檢查，所以實際花費會超過一點；超過時這一輪以 <c>error_max_budget_usd</c>
    /// （<see cref="Messages.ResultMessage.Subtype"/>）結束，之後送的訊息也直接這樣結束，不再呼叫 API。沒指定就不限制。
    /// 必須大於 0，否則 <see cref="ClaudeAgentClient.ConnectAsync"/> 丟出 <see cref="ArgumentOutOfRangeException"/>。
    /// Python 是 <c>float</c>；這裡是金額，用 <see langword="decimal"/>。
    /// </summary>
    public decimal? MaxBudgetUsd { get; init; }

    /// <summary>
    /// 結構化輸出的 JSON Schema（<c>--json-schema</c>）。指定之後，符合 schema 的結果放在
    /// <see cref="Messages.ResultMessage.StructuredOutput"/>，可以用 <c>Deserialize&lt;T&gt;()</c> 轉成自己的型別。
    /// Claude Code 是讓模型呼叫 <c>StructuredOutput</c> 工具交出結果，會多用掉模型來回的次數（簡單的問題實測用了 3 次），
    /// 同時設定 <see cref="MaxTurns"/> 時不要設太小。
    /// 對應 Python 的 <c>output_format</c>：Python 寫成 <c>{"type": "json_schema", "schema": ...}</c>，
    /// 目前只有 <c>json_schema</c> 一種，所以這裡直接放 schema。
    /// </summary>
    public JsonObject? OutputSchema { get; init; }

    /// <summary>
    /// 工具要執行前、需要使用者批准時呼叫。設定之後 CLI 改用 control 協定詢問（<c>--permission-prompt-tool stdio</c>）。
    /// 等待期間 CLI 撤回詢問（例如這一輪被中斷），<see cref="CancellationToken"/> 會被取消，這時回傳什麼都不會送出。
    /// </summary>
    public Func<ToolPermissionRequest, CancellationToken, Task<PermissionResult>>? CanUseTool { get; init; }

    /// <summary>額外的環境變數，蓋過繼承自目前程序的同名變數。對應 Python 的 <c>env</c>。</summary>
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; } = ReadOnlyDictionary<string, string>.Empty;

    /// <summary>
    /// 額外的 CLI 參數，給這個類別還沒有對應屬性的旗標用，例如 <c>add-dir</c>、<c>permission-mode</c>、<c>mcp-config</c>。
    /// key 是旗標名稱，不含開頭的 <c>--</c>；value 是 <see langword="null"/> 時只帶旗標、不帶值。
    /// 值整個當成一個參數傳，可以是含引號的 JSON；<c>-</c> 開頭的值寫成 <c>--旗標=值</c>，免得被 CLI 當成另一個旗標。
    /// 已經有對應屬性的旗標請用屬性設定。<c>output-format</c>、<c>input-format</c> 由這個套件控制，不能放；
    /// 設定了 <see cref="CanUseTool"/> 時，<c>permission-prompt-tool</c> 也不能放。
    /// 對應 Python 的 <c>extra_args</c>。
    /// </summary>
    public IReadOnlyDictionary<string, string?> ExtraArguments { get; init; } = ReadOnlyDictionary<string, string?>.Empty;

    /// <summary>
    /// 每收到一行 CLI 的 stderr 就呼叫一次，空行不算。沒指定時不導向 stderr，CLI 直接寫到目前程序的 stderr。
    /// 對應 Python 的 <c>stderr</c>。
    /// </summary>
    public Action<string>? StandardErrorCallback { get; init; }

    /// <summary>
    /// 這個套件自己的警告，例如 Claude Code 的版本低於支援的最低版本。只是提醒，不會中斷連線。
    /// 有指定時，連線前會先執行一次 <c>claude -v</c> 檢查版本；環境變數 <c>CLAUDE_AGENT_SDK_SKIP_VERSION_CHECK</c> 設成任何值就不檢查。
    /// Python SDK 沒有這個選項，警告寫進 <c>logging</c>。
    /// </summary>
    public Action<string>? WarningCallback { get; init; }

    /// <summary>
    /// 單一則訊息的大小上限（位元組）。超過就丟出 <see cref="CliJsonDecodeException"/>，避免異常的輸出吃光記憶體。
    /// </summary>
    public int MaxBufferSize { get; init; } = 1024 * 1024;
}
