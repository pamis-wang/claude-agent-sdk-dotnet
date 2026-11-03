# Changelog

## 0.1.0-preview.8（2026-11-03）

- `ClaudeAgentOptions.MaxTurns`：模型來回次數的上限（`--max-turns`），每次送出訊息各自計算；超過時 `ResultMessage.Subtype` 是 `error_max_turns`。
- `ClaudeAgentOptions.MaxBudgetUsd`：API 費用的上限，單位美元（`--max-budget-usd`），整個 session 累計；每次呼叫完 API 才檢查，
  超過時 `Subtype` 是 `error_max_budget_usd`，之後的訊息也直接這樣結束。Python 是 `float`，這裡是金額，用 `decimal`。
- 這兩個都必須大於 0，否則 `ConnectAsync` 丟 `ArgumentOutOfRangeException`。Python 遇到 0 會略過旗標、負數照送
  （送出 `-` 開頭、會被 CLI 當成另一個旗標的值）。數字照 InvariantCulture 寫，小數點是逗號的語系也送出 `1.25`。
- `ClaudeAgentOptions.OutputSchema`：結構化輸出的 JSON Schema（`--json-schema`，`JsonObject`），結果在 `ResultMessage.StructuredOutput`。
  對應 Python 的 `output_format`；Python 要寫成 `{"type": "json_schema", "schema": ...}`，目前只有這一種，所以這裡直接放 schema。
- CLI 參數的順序跟 Python 一樣：`--max-turns`、`--max-budget-usd` 在 `--disallowedTools` 前面，`--json-schema` 在 `--effort` 後面。
- `ResultMessage.Subtype` 的說明補上 `error_max_budget_usd`、`error_max_structured_output_retries`；
  `TotalCostUsd` 的說明註明是整個 session 累計的費用。
- 以上行為用 CLI 2.1.309 與 haiku 實測過：結構化輸出是模型呼叫 `StructuredOutput` 工具交出結果，簡單的問題也用了 3 次模型來回。

## 0.1.0-preview.7（2026-11-03）

- `ClaudeAgentOptions.ExtraArguments`：額外的 CLI 參數，給還沒有專屬屬性的旗標用（例如 `max-turns`、`add-dir`、`mcp-config`），
  對應 Python SDK 的 `extra_args`。key 是旗標名稱，不含 `--`；value 是 `null` 時只帶旗標。
  跟 Python 一樣排在 `--effort` 前面，`-` 開頭的值寫成 `--旗標=值`。
  Python 不檢查的這裡會丟 `ArgumentException`：旗標名稱是空的、以 `-` 開頭或含 `=`，以及這個套件自己控制的
  `output-format`、`input-format`，還有設定 `CanUseTool` 時的 `permission-prompt-tool`。
- `ConnectAsync` 的說明補上 `ArgumentException`：選項組不出 CLI 參數時丟出，原本 Windows 上 `ResumeSessionId` 含 cmd.exe 特殊字元就會丟。

## 0.1.0-preview.6（2026-11-01）

- 函式庫沒有變更。重新發佈讓 NuGet 頁面換成新的 README。
- README 改成英文為主（GitHub 與 NuGet 預設顯示這份），中文版移到 `README.zh-TW.md`；
  新增「跟官方 SDK 比較」一節。
- 新增 `docs/`：跟官方 Python、TypeScript SDK 的功能對照，以及跟 PamisWang.CodexAgentSdk 的對照，各有英文與中文版。

## 0.1.0-preview.5（2026-11-01）

- `ClaudeAgentOptions.WarningCallback`：這個套件自己的警告。指定時連線前會執行 `claude -v`，
  版本低於 2.0.0 就警告，不中斷連線；對應 Python SDK 的 `_check_claude_version`，
  一樣可以用環境變數 `CLAUDE_AGENT_SDK_SKIP_VERSION_CHECK` 跳過。Python SDK 把警告寫進 `logging`，沒有這個選項。
- `ResultMessage.Result`、`IsError` 的說明補上 API 錯誤的情況：例如沒登入時 `Subtype` 仍是 `success`，
  `IsError` 是 `true`，`Result` 是錯誤訊息；錯誤的種類（例如 `authentication_failed`）見 `AssistantMessage.Error`。

## 0.1.0-preview.4（2026-10-31）

- **不相容變更**：`ClaudeAgentOptions` 的屬性改成 .NET 慣用的名稱，行為不變：`Cwd` → `WorkingDirectory`、
  `Env` → `EnvironmentVariables`、`Stderr` → `StandardErrorCallback`、`Resume` → `ResumeSessionId`。
- **不相容變更**：`ResultMessage` 的屬性改成 .NET 慣用的名稱，型別不變：`DurationMs` → `DurationMilliseconds`、
  `DurationApiMs` → `ApiDurationMilliseconds`、`NumTurns` → `TurnCount`。
- `ClaudeAgentOptions` 與 `ResultMessage` 的說明註明對應 Python SDK 的哪個欄位：同名的照 snake_case 對應，
  名稱不同的在屬性上寫出來。

## 0.1.0-preview.3（2026-10-31）

- 函式庫沒有變更。git 歷史改寫過（協定樣本去識別化），重新發佈讓套件記錄的 commit 對應到現在的歷史；
  preview.1、preview.2 記錄的是改寫前的 commit。
- 協定樣本的去識別化加上帳號的訂閱方案。

## 0.1.0-preview.2（2026-10-31）

- `ClaudeAgentOptions.SystemPrompt`：`SystemPromptPreset`（Claude Code 預設的系統提示詞，可以在後面附加一段）、
  `SystemPromptCustom`（自訂全文）、`SystemPromptFile`（從檔案讀）。沒指定時照舊傳空的系統提示詞，跟 Python SDK 一樣。
- `ClaudeAgentOptions.Effort`：思考深度（`--effort`）。
- `ClaudeAgentClient.SetModelAsync`：對話中途換模型（`set_model`），對應 Python 的 `set_model()`；
  另外回傳 CLI 換過去的完整模型 ID（Python 與 TypeScript SDK 不回傳）。
- `ClaudeAgentClient.ApplyFlagSettingsAsync`：對話中途套用設定（`apply_flag_settings`），例如換思考深度。
  Python SDK 沒有這個功能，照 TypeScript SDK v0.3.316 的 `applyFlagSettings()` 送出的協定格式寫。
- 協定樣本加上 `set_model`、`apply_flag_settings` 兩個情境（CLI 2.1.309）。

## 0.1.0-preview.1（2026-10-30）

- 專案骨架：函式庫、測試專案、打包設定。
- 對照 Python SDK v0.2.174、Claude Code CLI 2.1.312。
- 協定樣本：`tools/record_fixtures.py` 用 Python SDK 錄下九種情境的 stream-json 收發，CLI 為 2.1.309。
- Transport（內部）：找 CLI（Windows 只執行原生 `claude.exe`，拒絕批次檔）、組參數與環境變數、
  以子程序收發 stream-json（UTF-8、一行一則、單則大小上限）、stderr callback、收尾時逾時強制結束整個程序樹。
- 公開型別：`ClaudeAgentOptions`（`CliPath`、`Cwd`、`Model`、`Resume`、`IncludePartialMessages`、`DisallowedTools`、
  `Env`、`Stderr`、`MaxBufferSize`）與例外階層（`ClaudeAgentException`、`CliConnectionException`、`CliNotFoundException`、
  `CliProcessException`、`CliJsonDecodeException`）。
- 協定層（內部）：initialize 握手；讀取迴圈把 control 協定與一般訊息分開，濾掉 `sdk_host_only` 的 session state；
  送出 control request 並等回應（逾時、錯誤回應、輸出結束時立刻失敗）；回覆權限詢問時另開工作，不擋讀取迴圈，
  CLI 撤回詢問時取消回呼且不回覆；中斷；送出使用者訊息。
- 權限：`ClaudeAgentOptions.CanUseTool`，以及 `Permissions` 命名空間的 `ToolPermissionRequest`、
  `PermissionResult`（`PermissionResultAllow`、`PermissionResultDeny`）、`PermissionUpdate`、`PermissionRuleValue`。
- 訊息：`Messages` 命名空間的訊息型別（`AssistantMessage`、`UserMessage`、`SystemMessage`、`ResultMessage`、
  `StreamEvent`、`RateLimitEvent`、`ConversationResetMessage`、`UnknownMessage`）與內容區塊（`TextBlock`、
  `ThinkingBlock`、`ToolUseBlock`、`ToolResultBlock`、`ServerToolUseBlock`、`ServerToolResultBlock`、`UnknownContentBlock`），
  每個都保留原始 JSON（`Raw`）。parser 的必要欄位跟 Python SDK 一樣，缺了丟 `MessageParseException`；
  不認得的訊息型別與內容區塊原樣保留，不丟掉。
- `ClaudeAgentClient`：`ConnectAsync`（啟動 CLI 並完成初始化握手）、`SendAsync`、`ReceiveMessagesAsync`、
  `ReceiveResponseAsync`、`InterruptAsync`、`ServerInfo`、`DisposeAsync`。一個 client 只連線一次。
