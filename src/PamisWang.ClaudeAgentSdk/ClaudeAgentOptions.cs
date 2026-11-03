using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using PamisWang.ClaudeAgentSdk.Mcp;
using PamisWang.ClaudeAgentSdk.Permissions;

namespace PamisWang.ClaudeAgentSdk;

/// <summary>
/// 啟動 Claude Code 的選項。對應 Python SDK 的 <c>ClaudeAgentOptions</c>，也包含 TypeScript SDK 才有的選項。
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

    /// <summary>
    /// 接續工作目錄裡最近的一段對話（<c>--continue</c>），不用知道 session ID。對應 Python 的 <c>continue_conversation</c>。
    /// 不能跟 <see cref="ResumeSessionId"/> 同時指定。
    /// </summary>
    public bool ContinueConversation { get; init; }

    /// <summary>
    /// 新 session 用這個 ID，不讓 CLI 自己產生（<c>--session-id</c>）；必須是 UUID。對應 Python 的 <c>session_id</c>。
    /// 搭配 <see cref="ResumeSessionId"/> 或 <see cref="ContinueConversation"/> 時，必須同時設定 <see cref="ForkSession"/>，用來指定分岔出來的新 session 的 ID。
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// 接續時建立新的 session ID，不沿用原本的（<c>--fork-session</c>）：原本的 session 不受影響，可以從同一點分岔出不同的方向。
    /// 要搭配 <see cref="ResumeSessionId"/> 或 <see cref="ContinueConversation"/>。
    /// </summary>
    public bool ForkSession { get; init; }

    /// <summary>
    /// 接續時只載入到這則訊息為止（<c>--resume-session-at</c>），包含它；之後的紀錄不載入。要搭配 <see cref="ResumeSessionId"/>。
    /// 值是紀錄裡任何一則的 UUID，通常是 <see cref="Messages.AssistantMessage"/> 的 <see cref="Messages.ClaudeMessage.Uuid"/>。
    /// </summary>
    public string? ResumeSessionAt { get; init; }

    /// <summary>
    /// 搭配 <see cref="ResumeSessionAt"/>：宣告這次截斷要丟掉的是哪一輪（那一輪使用者訊息的 UUID，<c>--resume-drops-turn</c>）。
    /// CLI 會檢查截斷點之後的紀錄是不是都屬於這一輪；如果混進了別的東西（例如那一輪中途排進來的訊息），就拒絕接續，
    /// 結果是 <c>error_during_execution</c>，訊息以 <c>Resume rejected by --resume-drops-turn:</c> 開頭。
    /// 被拒絕時不要重試同樣的請求（每次都會被拒絕），改成不截斷直接接續。沒指定就不檢查。
    /// </summary>
    public string? ResumeDropsTurn { get; init; }

    /// <summary>
    /// 是否把 session 存到 <c>~/.claude/projects</c>。預設 <see langword="true"/>；<see langword="false"/> 時不存（<c>--no-session-persistence</c>），之後也不能接續。
    /// Python SDK 沒有這個選項，對應 TypeScript SDK 的 <c>persistSession</c>。
    /// </summary>
    public bool PersistSession { get; init; } = true;

    /// <summary>
    /// 記錄檔案的變更，之後可以用 <see cref="ClaudeAgentClient.RewindFilesAsync"/> 把檔案還原到某則使用者訊息時的樣子。
    /// CLI 會在改檔之前先備份（環境變數 <c>CLAUDE_CODE_ENABLE_SDK_FILE_CHECKPOINTING</c>）。
    /// </summary>
    public bool EnableFileCheckpointing { get; init; }

    /// <summary>是否接收部分訊息（<c>--include-partial-messages</c>）：模型輸出時逐段送來的 <c>stream_event</c>。</summary>
    public bool IncludePartialMessages { get; init; }

    /// <summary>
    /// 可以用的內建工具（<c>--tools</c>）：只有列出來的工具會給模型，其他的模型看不到。
    /// 空的清單是停用所有內建工具；<see langword="null"/>（預設）是 Claude Code 預設的整組工具。
    /// 這是限制有哪些工具；要讓工具不經詢問就執行，用 <see cref="AllowedTools"/>。
    /// 有些版本的 Claude Code 用 Bash 的 <c>find</c>／<c>grep</c> 代替 Grep／Glob 工具，要用它們就列在這裡或 <see cref="AllowedTools"/>。
    /// </summary>
    public IReadOnlyList<string>? Tools { get; init; }

    /// <summary>
    /// 不經詢問、直接允許的工具（<c>--allowedTools</c>），可以寫成規則，例如 <c>Bash(git *)</c>。
    /// 這不會限制有哪些工具可以用；要限制，用 <see cref="Tools"/>。
    /// </summary>
    public IReadOnlyList<string> AllowedTools { get; init; } = [];

    /// <summary>停用的工具名稱（<c>--disallowedTools</c>）：從模型看得到的工具裡移除，即使其他設定允許也不能用。</summary>
    public IReadOnlyList<string> DisallowedTools { get; init; } = [];

    /// <summary>
    /// 開對話時的權限模式（<c>--permission-mode</c>）。沒指定時 CLI 自己決定：設定檔有 <c>permissions.defaultMode</c> 就用它，
    /// 沒有的話能用自動模式就用 <see cref="Permissions.PermissionMode.Auto"/>，不能用就是 <see cref="Permissions.PermissionMode.Default"/>。
    /// 要一律經 <see cref="CanUseTool"/> 批准，明確指定 <see cref="Permissions.PermissionMode.Default"/>。
    /// 連線後可以用 <see cref="ClaudeAgentClient.SetPermissionModeAsync"/> 換。
    /// </summary>
    public PermissionMode? PermissionMode { get; init; }

    /// <summary>
    /// 讓這個 session 可以切到 <see cref="Permissions.PermissionMode.BypassPermissions"/>（<c>--allow-dangerously-skip-permissions</c>），但不會直接開啟。
    /// 只適合沒有網路的沙箱。Python SDK 沒有這個選項，對應 TypeScript SDK 的 <c>allowDangerouslySkipPermissions</c>。
    /// </summary>
    public bool AllowDangerouslySkipPermissions { get; init; }

    /// <summary>
    /// 用哪個 MCP 工具回答權限詢問（<c>--permission-prompt-tool</c>），例如 <c>mcp__approver__ask</c>。
    /// 不能跟 <see cref="CanUseTool"/> 同時設定：設定 <see cref="CanUseTool"/> 時固定走 stdio。
    /// </summary>
    public string? PermissionPromptToolName { get; init; }

    /// <summary>
    /// 沒有人回答權限詢問（<c>--permission-prompts none</c>）：權限模式、規則與 hook 照常判斷，原本會詢問的動作直接拒絕，
    /// 並告訴模型這個 session 沒有批准的介面。不能跟 <see cref="CanUseTool"/>、<see cref="PermissionPromptToolName"/> 同時設定。
    /// Python SDK 沒有這個選項，對應 TypeScript SDK 的 <c>permissionPrompts: 'none'</c>。
    /// </summary>
    public bool DisablePermissionPrompts { get; init; }

    /// <summary>
    /// <c>AskUserQuestion</c> 工具的選項預覽用什麼格式：<c>markdown</c> 或 <c>html</c>（環境變數 <c>CLAUDE_CODE_QUESTION_PREVIEW_FORMAT</c>）。
    /// 網頁上顯示問題時用 <c>html</c>。沒指定就用 Claude Code 的預設。
    /// Python SDK 沒有這個選項，對應 TypeScript SDK 的 <c>toolConfig.askUserQuestion.previewFormat</c>。
    /// </summary>
    public string? AskUserQuestionPreviewFormat { get; init; }

    /// <summary>
    /// 外部 MCP server，key 是 server 名稱（<c>--mcp-config</c>）。設定檔與 <c>.mcp.json</c> 裡的 server 也會載入，
    /// 只要這裡的就設定 <see cref="StrictMcpConfig"/>。對應 Python 的 <c>mcp_servers</c>；設定檔的路徑可以用 <see cref="ExtraArguments"/> 的 <c>mcp-config</c> 帶。
    /// </summary>
    public IReadOnlyDictionary<string, McpServerConfig> McpServers { get; init; } = ReadOnlyDictionary<string, McpServerConfig>.Empty;

    /// <summary>
    /// 只用 <see cref="McpServers"/> 的 MCP server（<c>--strict-mcp-config</c>）：專案的 <c>.mcp.json</c>、使用者設定、外掛與 subagent 定義裡的都不載入。
    /// </summary>
    public bool StrictMcpConfig { get; init; }

    /// <summary>
    /// 主對話用哪個 agent（<c>--agent</c>）：套用那個 agent 的系統提示詞、工具限制與模型。agent 要定義在設定檔裡。
    /// Python SDK 沒有這個選項，對應 TypeScript SDK 的 <c>agent</c>。
    /// </summary>
    public string? Agent { get; init; }

    /// <summary>
    /// 額外的設定，鍵與值跟 Claude Code 的 <c>settings.json</c> 一樣（<c>--settings</c>），放在 flag 設定層：
    /// 優先於使用者、專案、本機的設定檔，低於組織的管理設定。對應 Python 的 <c>settings</c>（JSON 字串）。
    /// 不能跟 <see cref="SettingsFile"/> 同時設定；跟 <see cref="Sandbox"/> 同時設定時，這裡不能有 <c>sandbox</c>。
    /// </summary>
    public JsonObject? Settings { get; init; }

    /// <summary>從檔案讀額外的設定（<c>--settings</c>），效果跟 <see cref="Settings"/> 一樣。對應 Python 的 <c>settings</c>（檔案路徑）。不能跟 <see cref="Sandbox"/> 同時設定。</summary>
    public string? SettingsFile { get; init; }

    /// <summary>
    /// 執行指令的沙箱設定，鍵與值跟 <c>settings.json</c> 的 <c>sandbox</c> 一樣，例如 <c>{"enabled": true, "autoAllowBashIfSandboxed": true}</c>。
    /// 會併進 <c>--settings</c> 的 <c>sandbox</c>。對應 Python 的 <c>sandbox</c>。
    /// </summary>
    public JsonObject? Sandbox { get; init; }

    /// <summary>
    /// 由啟動的應用程式提供的管理設定層（<c>--managed-settings</c>），給需要強制套用限制的應用程式用。
    /// 電腦上已經有組織的管理設定時，預設會被丟掉，除非管理員允許；即使套用，也只保留更嚴格的部分。
    /// Python SDK 沒有這個選項，對應 TypeScript SDK 的 <c>managedSettings</c>。
    /// </summary>
    public JsonObject? ManagedSettings { get; init; }

    /// <summary>
    /// 要載入哪些設定檔（<c>--setting-sources</c>）。<see langword="null"/>（預設）是全部載入，跟在終端機用 Claude Code 一樣；
    /// 空的清單是都不載入。要載入 CLAUDE.md 必須包含 <see cref="SettingSource.Project"/>。對應 Python 的 <c>setting_sources</c>。
    /// </summary>
    public IReadOnlyList<SettingSource>? SettingSources { get; init; }

    /// <summary>這個 session 要載入的本機外掛（<c>--plugin-dir</c>）。對應 Python 的 <c>plugins</c>。</summary>
    public IReadOnlyList<LocalPlugin> Plugins { get; init; } = [];

    /// <summary>
    /// <see cref="Plugins"/> 怎麼交給 CLI，預設是命令列參數。外掛很多、在 Windows 上可能超過命令列長度時用 <see cref="ClaudeAgentSdk.PluginDelivery.Initialize"/>。
    /// Python SDK 沒有這個選項，對應 TypeScript SDK 的 <c>pluginDelivery</c>。
    /// </summary>
    public PluginDelivery PluginDelivery { get; init; } = PluginDelivery.CommandLine;

    /// <summary>
    /// 主對話可以用哪些 skill：<see cref="SkillSelection.All"/> 或 <see cref="SkillSelection.Only"/>。會自動在 <see cref="AllowedTools"/> 加上對應的 <c>Skill</c> 規則，
    /// 名稱清單也放進初始化握手。<see langword="null"/>（預設）是不設定，照 Claude Code 自己的預設，不等於關掉 skill。
    /// 對應 Python 的 <c>skills</c>；Python 還會在 <see cref="SettingSources"/> 沒指定時改成只載入使用者與專案的設定，這裡照 TypeScript SDK 不動它。
    /// </summary>
    public SkillSelection? Skills { get; init; }

    /// <summary>
    /// 自訂的 subagent，key 是 agent 名稱，主對話可以用 Agent 工具交辦工作給它們。放在初始化握手裡送，不經過命令列。
    /// 對應 Python 的 <c>agents</c>。
    /// </summary>
    public IReadOnlyDictionary<string, AgentDefinition> Agents { get; init; } = ReadOnlyDictionary<string, AgentDefinition>.Empty;

    /// <summary>
    /// 把 subagent 的文字與思考也轉送出來（以 <see cref="Messages.AssistantMessage"/>、<see cref="Messages.UserMessage"/> 送，<c>ParentToolUseId</c> 是交辦的那個工具呼叫）。
    /// 預設只送 subagent 的工具呼叫與結果；要顯示完整的巢狀對話時打開。對應 Python 的 <c>forward_subagent_text</c>。
    /// </summary>
    public bool ForwardSubagentText { get; init; }

    /// <summary>
    /// 每隔約 30 秒為執行中的 subagent 產生一句進度摘要（例如「正在分析驗證模組」），放在 <see cref="Messages.TaskProgressMessage.Summary"/>。
    /// 沿用 subagent 的模型與快取，費用通常很少。Python SDK 沒有這個選項，對應 TypeScript SDK 的 <c>agentProgressSummaries</c>。
    /// </summary>
    public bool AgentProgressSummaries { get; init; }

    /// <summary>
    /// 宣告畫面上有每個背景任務各自的停止按鈕（接到 <see cref="ClaudeAgentClient.StopTaskAsync"/>）。
    /// 宣告之後，<see cref="ClaudeAgentClient.InterruptAsync"/> 只中斷這一輪，不會一起停掉背景的 subagent 與 workflow；
    /// 沒宣告時中斷會停掉背景任務，免得失控的任務停不下來。Python SDK 沒有這個選項，對應 TypeScript SDK 的 <c>perTaskStopAffordance</c>。
    /// </summary>
    public bool PerTaskStopAffordance { get; init; }

    /// <summary>
    /// 工具名稱的別名：模型呼叫的工具名稱是這裡的 key 時，改用 value 的工具執行，例如 <c>{"Bash": "mcp__workspace__bash"}</c> 把 Bash 導到自己的 MCP 工具。
    /// 只轉一次，不會連鎖。這只影響模型呼叫工具時的名稱查找，要確實擋掉工具還是要用 <see cref="DisallowedTools"/>。
    /// Python SDK 沒有這個選項，對應 TypeScript SDK 的 <c>toolAliases</c>。
    /// </summary>
    public IReadOnlyDictionary<string, string> ToolAliases { get; init; } = ReadOnlyDictionary<string, string>.Empty;

    /// <summary>
    /// 規劃模式（<see cref="Permissions.PermissionMode.Plan"/>）的工作流程說明，取代預設的「規劃怎麼寫程式」那段；
    /// CLI 仍會加上唯讀的限制與 ExitPlanMode 的說明。Python SDK 沒有這個選項，對應 TypeScript SDK 的 <c>planModeInstructions</c>。
    /// </summary>
    public string? PlanModeInstructions { get; init; }

    /// <summary>
    /// 使用者訊息照原文送給模型（每則訊息帶 <c>client_composed: true</c>）：不展開 <c>@路徑</c> 的檔案、不執行斜線指令。
    /// 訊息不是使用者自己打的（例如程式組出來的）時用。CLI 也會略過這一輪開始時附加的 context（巢狀的 CLAUDE.md、skill 清單等），
    /// 這些會在第一次呼叫工具之後才補上。需要 Claude Code 2.1.248 以後的版本，舊版會忽略。對應 Python 的 <c>verbatim_prompts</c>。
    /// </summary>
    public bool VerbatimPrompts { get; init; }

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
    /// API 端的任務預算，單位 token（<c>--task-budget</c>）：讓模型知道還剩多少預算，自己控制工具的使用並在用完前收尾。
    /// 必須大於 0，否則 <see cref="ClaudeAgentClient.ConnectAsync"/> 丟出 <see cref="ArgumentOutOfRangeException"/>。
    /// 對應 Python 的 <c>task_budget</c>（Python 寫成 <c>{"total": ...}</c>，目前只有這個欄位，所以這裡直接放數字）。
    /// </summary>
    public int? TaskBudget { get; init; }

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
    /// 設定了 <see cref="CanUseTool"/> 或 <see cref="PermissionPromptToolName"/> 時，<c>permission-prompt-tool</c> 也不能放。
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
