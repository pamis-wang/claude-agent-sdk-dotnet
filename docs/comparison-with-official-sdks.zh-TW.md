# claude-agent-sdk-dotnet 與官方 Python / TypeScript SDK 對照

[English](comparison-with-official-sdks.md) | 繁體中文

對照 `PamisWang.ClaudeAgentSdk` 與 Anthropic 官方的 Claude Agent SDK（Python、TypeScript），看移植涵蓋了哪些功能、刻意不做哪些、又多了什麼。

- 調查日期：2026-11-03
- 官方 Python：claude-agent-sdk 0.2.177（8abce11）
- 官方 TypeScript：@anthropic-ai/claude-agent-sdk 0.3.317
- .NET：PamisWang.ClaudeAgentSdk 0.1.0-preview.10
- .NET 版對照 Python 0.2.174；0.2.175、0.2.176 只更新打包的 Claude CLI（2.1.314、2.1.316）；0.2.177 打包 CLI 2.1.317，新增的 `HookMatcher.on_failure` 屬於 .NET 還沒做的 hook。.NET 用到的 API 都沒有變動，所以功能面仍以最新版對照。TypeScript 欄位依套件內的 `sdk.d.ts` 型別判斷。

相關文件：[Claude .NET vs Codex .NET](claude-vs-codex-dotnet.zh-TW.md)。[Codex .NET 與官方 SDK 的對照](https://github.com/pamis-wang/codex-agent-sdk-dotnet/blob/main/docs/comparison-with-official-sdks.zh-TW.md)在 codex-agent-sdk-dotnet repo。

## 重點

1. **定位是互動式 client 的子集移植**：.NET 版對應 Python 的 `ClaudeSDKClient`（連線、送訊息、收回覆、批准工具、中斷、接續 session），沒有一次性的 `query()` 函式，也沒有公開的自訂 Transport。
2. **對話與 session、工具權限與擴充兩塊已經補齊**：Python `ClaudeAgentOptions` 有 49 個欄位、TypeScript `Options` 有 69 個；.NET `ClaudeAgentOptions` 有 50 個。這兩塊裡 Python 有的都有了，TypeScript 才有的（例如 `toolAliases`、`managedSettings`、`perTaskStopAffordance`、`setMcpServers()`、`reloadPlugins()`）也照它送給 CLI 的格式補上，並用 CLI 2.1.312 實測過。沒有專屬選項的 CLI 旗標可以用 `ExtraArguments` 帶。
3. **還沒做、之後會補的三塊**：hook 回呼、讀寫 `~/.claude/projects` 的 session 函式、SDK 內的 MCP server（自訂工具）。**刻意不做**：session store、transcript mirror。
4. **比官方多的地方**：`ApplyFlagSettingsAsync`（Python 沒有，照 TS 協定）、`SetModelAsync` 與 `SetPermissionModeAsync` 回傳 CLI 實際換過去的值、`GetContextUsageAsync` 的 `detail`、`RewindFilesAsync` 回傳結果與 `dryRun`（Python 沒有）、`WarningCallback`、每則訊息保留 `Raw` 且不丟掉不認得的訊息（Python 直接略過）、互相衝突的選項在連線前就丟例外、Windows 拒絕執行 `.cmd`、AOT 相容。
5. **測試比較紮實**：325 個測試，含從真實 CLI 錄下來的協定樣本（`Fixtures/*.jsonl`）與假 CLI 子程序測試。
6. **授權**：Python 是 MIT，可以移植；TypeScript 是專有授權（© Anthropic），.NET 版只參考它送出的協定格式，沒有移植程式碼。

## 功能矩陣

圖例：✅ 支援、⭐ 加強、🟡 部分、❌ 無、— 不適用（⭐ 表示比官方多做的部分）

只是 CLI 旗標的功能（例如 `--add-dir`、`--fallback-model`）標 ❌，表示 .NET 沒有專屬的選項；這些旗標還是可以用 `ExtraArguments` 帶。

### 進入點與連線

| 功能 | 官方 Python（claude-agent-sdk 0.2.177） | 官方 TypeScript（claude-agent-sdk 0.3.317） | .NET（PamisWang.ClaudeAgentSdk） |
|---|---|---|---|
| **一次性查詢** | ✅ `query(prompt, options)`，prompt 可以是字串或非同步串流 | ✅ `query({ prompt, options })` | ❌ 改用 `ClaudeAgentClient` |
| **互動式 client** | ✅ `ClaudeSDKClient`：`connect`、`query`、`receive_response`、`disconnect` | 🟡 `query()` 回傳的 `Query` 物件，加上 `streamInput` | ✅ `ClaudeAgentClient`：`ConnectAsync`、`SendAsync`、`ReceiveResponseAsync`、`DisposeAsync` |
| **自訂 Transport** | ✅ 公開的 `Transport` 抽象類別 | 🟡 `spawnClaudeCodeProcess` 自訂啟動方式 | ❌ `ITransport` 只在內部 |
| **CLI 路徑** | ✅ `cli_path` | ✅ `pathToClaudeCodeExecutable`、`executable`、`executableArgs` | ⭐ `CliPath`；Windows 拒絕 npm 的 `claude.cmd`（指令注入風險） |
| **附帶 CLI** | ✅ 套件內附 Claude CLI（`_bundled`） | ✅ 套件內附 | ❌ 要自己安裝原生的 `claude.exe` |
| **版本檢查** | 🟡 `_check_claude_version`，警告寫進 `logging` | — 型別定義看不出來 | ⭐ `WarningCallback`；`CLAUDE_AGENT_SDK_SKIP_VERSION_CHECK` 可跳過 |
| **環境變數** | ✅ `env` | ✅ `env` | ✅ `EnvironmentVariables` |
| **額外 CLI 參數** | ✅ `extra_args` | ✅ `extraArgs` | ⭐ `ExtraArguments`；另外擋下寫錯的旗標名稱與 SDK 自己控制的旗標 |
| **工作目錄** | ✅ `cwd`、`add_dirs` | ✅ `cwd`、`additionalDirectories` | 🟡 `WorkingDirectory`（沒有額外目錄） |
| **stderr** | ✅ `stderr` | ✅ `stderr` | ✅ `StandardErrorCallback` |
| **訊息大小上限** | ✅ `max_buffer_size` | — | ✅ `MaxBufferSize` |

### 對話與 session

| 功能 | 官方 Python（claude-agent-sdk 0.2.177） | 官方 TypeScript（claude-agent-sdk 0.3.317） | .NET（PamisWang.ClaudeAgentSdk） |
|---|---|---|---|
| **接續 session** | ✅ `resume`、`continue_conversation`、`session_id`、`resume_session_at`、`resume_drops_turn` | ✅ `resume`、`continue`、`sessionId`、`resumeSessionAt`、`resumeDropsTurn` | ⭐ `ResumeSessionId`、`ContinueConversation`、`SessionId`、`ResumeSessionAt`、`ResumeDropsTurn`；互相衝突的組合連線前就丟例外 |
| **分岔 session** | ✅ `fork_session` 選項與函式 | ✅ `forkSession` | 🟡 `ForkSession` 選項；分岔用的函式還沒有 |
| **不存 session** | ❌ | ✅ `persistSession: false` | ✅ `PersistSession = false` |
| **照原文送出** | ✅ `verbatim_prompts` | ✅ `verbatimPrompts` | ✅ `VerbatimPrompts` |
| **列出 / 讀取歷史** | ✅ `list_sessions`、`get_session_info`、`get_session_messages`、`list_subagents` | ✅ `listSessions`、`getSessionMessages` 等 | ❌ 之後會補 |
| **改名 / 標籤 / 刪除** | ✅ `rename_session`、`tag_session`、`delete_session` | ✅ `renameSession`、`tagSession`、`deleteSession` | ❌ 之後會補 |
| **Session store / transcript mirror** | ✅ `session_store`、`InMemorySessionStore` | ✅ `sessionStore` | ❌ 刻意不做 |
| **中斷** | ✅ `interrupt()` | ✅ `interrupt()` | ✅ `InterruptAsync`；權限詢問中的 token 會被取消 |
| **部分訊息串流** | ✅ `include_partial_messages` | ✅ `includePartialMessages` | ⭐ `IncludePartialMessages`，另有 `StreamEvent.TextDelta` 取文字 |
| **輪數 / 預算上限** | ✅ `max_turns`、`max_budget_usd`、`task_budget` | ✅ `maxTurns`、`maxBudgetUsd`、`taskBudget` | ✅ `MaxTurns`、`MaxBudgetUsd`（金額是 `decimal`）、`TaskBudget` |
| **結構化輸出** | ✅ `output_format` | ✅ `outputFormat` | ✅ `OutputSchema`（`JsonObject`），結果在 `ResultMessage.StructuredOutput` |
| **檔案檢查點 / 回溯** | ✅ `enable_file_checkpointing`、`rewind_files` | ✅ `enableFileCheckpointing`、`rewindFiles`（可以 `dryRun`） | ⭐ `EnableFileCheckpointing`、`RewindFilesAsync`；回傳 `RewindFilesResult`、可以 `dryRun`（Python 兩者都沒有） |

### 模型與提示詞

| 功能 | 官方 Python（claude-agent-sdk 0.2.177） | 官方 TypeScript（claude-agent-sdk 0.3.317） | .NET（PamisWang.ClaudeAgentSdk） |
|---|---|---|---|
| **系統提示詞** | ✅ 字串、`SystemPromptPreset`、`SystemPromptCustom`、`SystemPromptFile` | ✅ `systemPrompt` | ✅ `SystemPromptPreset`、`SystemPromptCustom`、`SystemPromptFile` |
| **模型** | ✅ `model`、`fallback_model` | ✅ `model`、`fallbackModel` | 🟡 `Model`（沒有備援模型） |
| **對話中換模型** | ✅ `set_model()` | ✅ `setModel()` | ⭐ `SetModelAsync`，另外回傳 CLI 換過去的完整模型 ID |
| **思考深度** | ✅ `effort`、`thinking`、`max_thinking_tokens` | ✅ `effort`、`thinking`、`setMaxThinkingTokens()` | 🟡 `Effort`（沒有 thinking 設定） |
| **對話中套用設定** | ❌ | ✅ `applyFlagSettings()`、`updateSettings()` | ⭐ `ApplyFlagSettingsAsync`，照 TS 送出的協定格式 |
| **Betas** | ✅ `betas` | ✅ `betas` | ❌ |
| **可用模型 / 指令** | 🟡 `get_server_info()` | ✅ `supportedModels()`、`supportedCommands()`、`supportedAgents()` | 🟡 `ServerInfo`（原始 JSON） |

### 工具、權限與擴充

| 功能 | 官方 Python（claude-agent-sdk 0.2.177） | 官方 TypeScript（claude-agent-sdk 0.3.317） | .NET（PamisWang.ClaudeAgentSdk） |
|---|---|---|---|
| **工具權限回呼** | ✅ `can_use_tool` | ✅ `canUseTool` | ✅ `CanUseTool`；回傳 `request.Suggestions` 即「一律允許」 |
| **允許 / 停用工具** | ✅ `tools`、`allowed_tools`、`disallowed_tools` | ✅ `tools`、`allowedTools`、`disallowedTools`、`toolAliases`、`toolConfig` | ✅ `Tools`、`AllowedTools`、`DisallowedTools`、`ToolAliases`、`AskUserQuestionPreviewFormat`（TS 的 `toolConfig` 目前只有這一項） |
| **權限模式** | ✅ `permission_mode`、`set_permission_mode()` | ✅ `permissionMode`、`setPermissionMode()`、`allowDangerouslySkipPermissions`、`planModeInstructions` | ⭐ `PermissionMode`（字串包成的型別，CLI 新增的模式也能帶）、`SetPermissionModeAsync` 回傳生效的模式、`AllowDangerouslySkipPermissions`、`PlanModeInstructions` |
| **權限提示工具** | ✅ `permission_prompt_tool_name` | ✅ `permissionPromptToolName`、`permissionPrompts` | ✅ `PermissionPromptToolName`、`DisablePermissionPrompts`；跟 `CanUseTool` 同時設定時丟例外 |
| **Hook 回呼** | ✅ `hooks`（`HookMatcher`，二十多種事件；`on_failure` 決定回呼失敗時要不要擋下動作） | ✅ `hooks`（matcher 可設 `onFailure`） | ❌ 之後會補 |
| **SDK 內 MCP server** | ✅ `create_sdk_mcp_server`、`@tool` | ✅ `createSdkMcpServer`、`tool` | ❌ 之後會補；現在可以自己開 MCP server（例如 ASP.NET Core 裡的 HTTP 端點），用 `McpServers` 接上 |
| **外部 MCP server** | ✅ `mcp_servers`、`strict_mcp_config` | ✅ `mcpServers`、`setMcpServers()` | ✅ `McpServers`（stdio、SSE、HTTP，含 TS 才有的 `timeout`、`alwaysLoad`、工具權限政策）、`StrictMcpConfig`、`SetMcpServersAsync` |
| **MCP 狀態 / 重連 / 開關** | ✅ `get_mcp_status`、`reconnect_mcp_server`、`toggle_mcp_server` | ✅ `mcpServerStatus()`、`reconnectMcpServer()`、`toggleMcpServer()`、`readMcpResource()`、`setMcpPermissionModeOverride()` | ✅ `GetMcpStatusAsync`（型別化的 `McpServerStatus`）、`ReconnectMcpServerAsync`、`ToggleMcpServerAsync`、`ReadMcpResourceAsync`、`SetMcpPermissionModeOverrideAsync` |
| **Subagent 定義** | ✅ `agents`（`AgentDefinition`）、`forward_subagent_text` | ✅ `agents`、`agent`、`forwardSubagentText`、`agentProgressSummaries` | ✅ `Agents`（`AgentDefinition`，含 TS 才有的欄位）、`Agent`、`ForwardSubagentText`、`AgentProgressSummaries` |
| **背景任務** | ✅ `stop_task()`、`Task*Message` | ✅ `stopTask()`、`backgroundTasks()`、`perTaskStopAffordance` | ✅ `StopTaskAsync`、`BackgroundTasksAsync`、`PerTaskStopAffordance`；`TaskStartedMessage` 等四種訊息加上 TS 才有的 `BackgroundTasksChangedMessage`，都是 `SystemMessage` 的子型別 |
| **沙箱設定** | ✅ `sandbox` | ✅ `sandbox` | ✅ `Sandbox`（`JsonObject`，併進 `--settings`；跟 `Settings` 都有 `sandbox` 時丟例外，不像 TS 逐鍵合併） |
| **設定來源 / plugins / skills** | ✅ `setting_sources`、`settings`、`plugins`、`skills` | ✅ `settingSources`、`settings`、`managedSettings`、`plugins`、`pluginDelivery`、`skills`、`reloadPlugins()`、`reloadSkills()` | ✅ `SettingSources`、`Settings`／`SettingsFile`、`ManagedSettings`、`Plugins`（含 `SkipMcpDiscovery`）、`PluginDelivery`、`Skills`、`ReloadPluginsAsync`、`ReloadSkillsAsync` |
| **Context 用量** | ✅ `get_context_usage()` | ✅ `getContextUsage()`，可選 `detail: 'summary' \| 'full'` | ⭐ `GetContextUsageAsync`，回傳型別化的 `ContextUsage`（含 Python 型別沒有的類別 `id`、`kind`）；`detail` 照 TS |

### 訊息與錯誤

| 功能 | 官方 Python（claude-agent-sdk 0.2.177） | 官方 TypeScript（claude-agent-sdk 0.3.317） | .NET（PamisWang.ClaudeAgentSdk） |
|---|---|---|---|
| **主要訊息型別** | ✅ User、Assistant、System、Result、StreamEvent、RateLimitEvent 等 | ✅ `SDKMessage` 共 40 種 | ✅ `UserMessage`、`AssistantMessage`、`SystemMessage`、`ResultMessage`、`StreamEvent`、`RateLimitEvent`、`ConversationResetMessage` |
| **內容區塊** | ✅ Text、Thinking、ToolUse、ToolResult、ServerToolUse、ServerToolResult | ✅ | ✅ 同 Python，另有 `UnknownContentBlock` |
| **不認得的訊息** | 🟡 直接略過（只記 debug log） | — 型別定義看不出來 | ⭐ `UnknownMessage`，不丟掉 |
| **保留原始 JSON** | ❌ | ❌ | ⭐ 每則訊息與內容區塊都有 `Raw` |
| **例外型別** | ✅ `ClaudeSDKError`、`CLIConnectionError`、`CLINotFoundError`、`ProcessError`、`CLIJSONDecodeError` | 🟡 `AbortError` | ✅ `ClaudeAgentException`、`CliConnectionException`、`CliNotFoundException`、`CliProcessException`、`CliJsonDecodeException`、`MessageParseException` |

## 規模與品質

| 項目 | 官方 Python | 官方 TypeScript | .NET |
|---|---|---|---|
| 程式碼 | 約 13,500 行 Python | 封裝成 bundle，型別定義 `sdk.d.ts` 約 10,500 行 | 手寫約 4,960 行 C#（82 個檔案） |
| 公開的 client 方法 | `ClaudeSDKClient` 15 個 | `Query` 31 個 | `ClaudeAgentClient` 20 個非同步方法 |
| 選項數 | 49 | 69 | 50 |
| 測試 | pytest | 未公開 | 325 個（xUnit），含真實 CLI 錄下的協定樣本與假 CLI 子程序 |
| 授權 | MIT | 專有（© Anthropic） | MIT，NOTICE 保留 Python SDK 的授權聲明 |
| AOT | — | — | `IsAotCompatible` |

## 建議的下一步

- **Hook 回呼**：CLI 送 `hook_callback`，可以共用 `CanUseTool` 用的分派、取消與錯誤回覆。有了權限模式之後，這是唯一能看到每一次工具呼叫的地方（`CanUseTool` 只在 CLI 本來會詢問時才被呼叫）。
- **Session 函式**：先做唯讀的（列出、查單筆、讀紀錄、讀 subagent 的紀錄），再做會寫入的（改名、加標籤、刪除、分岔）。它們直接讀寫 Claude Code 的 jsonl，Python 這部分約 2,500 行。
- **SDK 內 MCP server**：CLI 送 `mcp_message`（JSON-RPC），SDK 要扮演 MCP server；AOT 下不能用反射產生工具的 schema。
- 其他區塊還缺的選項：`AddDirectories`、`FallbackModel`、`Betas`、thinking 的設定，以及 TypeScript 才有的 `projectConfigRoot`、`promptSuggestions` 等。

