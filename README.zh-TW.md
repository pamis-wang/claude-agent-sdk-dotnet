# PamisWang.ClaudeAgentSdk

[English](README.md) | 繁體中文

Claude Code 的 .NET 用戶端：啟動本機的 `claude` CLI，用 stream-json 協定對話、批准工具、中斷、接續 session。

> **非官方**。移植自 [claude-agent-sdk-python](https://github.com/anthropics/claude-agent-sdk-python)，與 Anthropic 無關。官方的 Agent SDK 只有 Python 與 TypeScript。
> Python SDK 沒有、TypeScript SDK 才有的功能，照 TypeScript SDK 送給 CLI 的協定格式另外寫，並用實際的 CLI 驗證過；TypeScript SDK 不是開源授權，沒有移植它的程式碼。

## 狀態

預覽版已經發佈到 NuGet；1.0 之前 API 可能還會變。

```bash
dotnet add package PamisWang.ClaudeAgentSdk --prerelease
```

| 對照 | 版本 |
|---|---|
| Python SDK | v0.2.174 |
| TypeScript SDK | v0.3.311（Python 沒有的功能，照它送給 CLI 的協定格式） |
| Claude Code CLI | 2.1.310（Python SDK v0.2.174 打包的版本） |
| 錄協定樣本用的 CLI | 2.1.309；權限模式、subagent、MCP、檔案還原的樣本是 2.1.312 |

## 跟官方 SDK 比較

- 對應 Python 的 `ClaudeSDKClient`：多輪對話、批准工具、中斷、接續 session；沒有一次性的 `query()`。
- 50 個選項（Python 有 49 個、TypeScript 有 69 個，切法不完全一樣）：對話與 session、工具、權限與擴充這兩塊，Python 與 TypeScript 有的都有了，含 TypeScript 才有的 `ToolAliases`、`ManagedSettings`、`PerTaskStopAffordance` 等。還沒有專屬選項的 CLI 旗標可以用 `ExtraArguments` 帶。還沒做的見下方的[範圍](#範圍)。
- 比官方多：`ApplyFlagSettingsAsync`（Python 沒有）、`SetModelAsync` 與 `SetPermissionModeAsync` 回傳 CLI 實際換過去的值、`GetContextUsageAsync` 的 `detail`（Python 沒有）、`RewindFilesAsync` 回傳結果與 `dryRun`（Python 沒有）、`WarningCallback`、每則訊息都有 `Raw` 而且不丟掉不認得的訊息、互相衝突的選項在連線前就丟例外、Windows 拒絕執行 `.cmd`、AOT 相容。
- 完整對照：[與官方 Python、TypeScript SDK](docs/comparison-with-official-sdks.zh-TW.md)、[與 PamisWang.CodexAgentSdk](docs/claude-vs-codex-dotnet.zh-TW.md)

## 使用方式

```csharp
using System.Text.Json.Nodes;
using PamisWang.ClaudeAgentSdk;
using PamisWang.ClaudeAgentSdk.Mcp;
using PamisWang.ClaudeAgentSdk.Messages;
using PamisWang.ClaudeAgentSdk.Permissions;

var options = new ClaudeAgentOptions
{
    WorkingDirectory = @"D:\work\my-project",
    SystemPrompt = new SystemPromptPreset(), // Claude Code 預設的系統提示詞；不給是空的
    Model = "sonnet",
    Effort = "high",
    ResumeSessionId = null,               // 接續舊 session 時填 session ID
    IncludePartialMessages = true,        // 逐段收到輸出
    DisallowedTools = ["AskUserQuestion"],
    AllowedTools = ["Read", "Grep", "Bash(git status)"], // 不經詢問直接允許
    PermissionMode = PermissionMode.Default,  // 一律經 CanUseTool 批准；不給由 CLI 照設定決定
    MaxTurns = 20,                        // 模型來回次數的上限
    MaxBudgetUsd = 1.00m,                 // 費用上限（美元）
    McpServers = new Dictionary<string, McpServerConfig>
    {
        ["docs"] = new McpHttpServerConfig("http://127.0.0.1:5000/mcp"),
    },
    // 還沒有專屬選項的 CLI 旗標；null 表示只帶旗標、不帶值
    ExtraArguments = new Dictionary<string, string?> { ["add-dir"] = @"D:\work\shared" },
    // 工具要執行前詢問；中斷時 cancellationToken 會被取消
    CanUseTool = async (request, cancellationToken) =>
        await AskUserAsync(request.ToolName, request.Input, cancellationToken)
            ? new PermissionResultAllow(UpdatedPermissions: request.Suggestions) // 一律允許
            : new PermissionResultDeny("使用者拒絕了"),
};

await using var client = new ClaudeAgentClient(options);
await client.ConnectAsync();
await client.SendAsync("這個專案在做什麼？");

await foreach (var message in client.ReceiveResponseAsync())
{
    switch (message)
    {
        case StreamEvent { TextDelta: { } delta }:
            Console.Write(delta);
            break;
        case AssistantMessage assistant:
            foreach (var tool in assistant.Content.OfType<ToolUseBlock>())
            {
                Console.WriteLine($"\n[{tool.Name}]");
            }

            break;
        case TaskStartedMessage task:             // subagent、背景指令開始了；可以用 StopTaskAsync 停掉
            Console.WriteLine($"\n[任務 {task.TaskId}] {task.Description}");
            break;
        case ResultMessage result:
            Console.WriteLine($"\n{result.Subtype}，{result.DurationMilliseconds} ms");
            break;
    }
}

// 接著說下一句，或中斷這一輪
await client.SendAsync("那測試怎麼跑？");
await client.InterruptAsync();

// 對話中途換模型、思考深度與權限模式
await client.SetModelAsync("opus");
await client.ApplyFlagSettingsAsync(new JsonObject { ["effortLevel"] = "max" });
await client.SetPermissionModeAsync(PermissionMode.AcceptEdits);

// MCP server 的連線狀態
foreach (var server in await client.GetMcpStatusAsync())
{
    Console.WriteLine($"{server.Name}：{server.Status}");
}

// context window 用了多少，依類別拆開，跟 /context 指令一樣
var usage = await client.GetContextUsageAsync();
Console.WriteLine($"{usage.RawMaxTokens} 個 token 用了 {usage.Percentage}%");
```

- 沒指定 `SystemPrompt` 時跟 Python SDK 一樣傳空的系統提示詞，Claude 不會拿到 Claude Code 預設的那份（寫程式的準則、工作目錄與 git 狀態等）；要跟在終端機用時一樣，指定 `SystemPromptPreset`。
- `MaxTurns` 每次送出訊息各自計算，超過時 `ResultMessage.Subtype` 是 `error_max_turns`。`MaxBudgetUsd` 是整個 session 累計（同 `TotalCostUsd`），每次呼叫完 API 才檢查，所以實際花費會超過一點；超過時是 `error_max_budget_usd`，之後的訊息也直接這樣結束。兩者都必須大於 0。
- 結構化輸出：`OutputSchema` 給 JSON Schema（`JsonObject`），結果在 `ResultMessage.StructuredOutput`，可以用 `Deserialize<T>()` 轉成自己的型別。Claude Code 是讓模型呼叫工具交出結果，會多用掉幾次模型來回，`MaxTurns` 不要設太小。
- `GetContextUsageAsync` 預設每個類別都用 token 計算 API 算，要等一下；`ContextUsageDetail.Summary` 幾乎馬上回來，但數字是估計值。找特定類別用 `Id`，分類用 `Kind`（`used`、`free`、`buffer`、`deferred`），不要用英文的 `Name`。`messageBreakdown`、`apiUsage` 這類沒有對應屬性的欄位從 `Raw` 讀。
- 沒指定 `PermissionMode` 時由 CLI 決定：設定檔有 `permissions.defaultMode` 就用它，沒有的話能用自動模式就是 `Auto`。要每個動作都經 `CanUseTool` 批准，明確指定 `PermissionMode.Default`。`PermissionMode` 是字串包成的型別，CLI 新增的模式可以用 `new PermissionMode("...")` 直接帶。
- `Tools` 是限制模型看得到哪些內建工具（空的清單是全部停用），`AllowedTools` 是哪些工具不經詢問直接允許，`DisallowedTools` 是移除；三者可以一起用。
- 選項互相衝突時 `ConnectAsync` 丟 `ArgumentException`，例如 `ContinueConversation` 與 `ResumeSessionId` 同時指定、`ForkSession` 沒有要接續的 session、`CanUseTool` 與 `PermissionPromptToolName` 同時設定。
- `ExtraArguments` 的 key 是旗標名稱，不含開頭的 `--`。值是 `-` 開頭時送成 `--旗標=值`，免得 CLI 把它當成另一個旗標。`output-format`、`input-format` 由這個套件控制，放了會丟例外；設定了 `CanUseTool` 或 `PermissionPromptToolName` 時，`permission-prompt-tool` 也一樣。
- 可以選的模型與各自支援的思考深度，在 `ServerInfo` 的 `models` 裡。
- `ReceiveResponseAsync` 讀到這一輪的 `ResultMessage` 為止；`ReceiveMessagesAsync` 跨越多輪一直讀，適合在背景持續接收。
- 每則訊息都有 `Raw`（原始 JSON），沒有對應屬性的欄位從這裡讀；不認得的訊息型別是 `UnknownMessage`，不會被丟掉。
- 一個 client 只連線一次；要重新開始就建立新的 client，用 `ResumeSessionId` 接續原本的 session。

## 範圍

支援：

- 多輪對話（stream-json 輸入），可以照原文送出、不展開 `@檔案` 與斜線指令（`VerbatimPrompts`）
- 接續 session：指定的（`--resume`）、最近的（`--continue`）、分岔成新的 session（`--fork-session`）、指定新 session 的 ID（`--session-id`）、只載入到某一則（`--resume-session-at`，可以用 `--resume-drops-turn` 檢查），也可以不存 session（`--no-session-persistence`）
- 檔案檢查點與還原（`rewind_files`，可以只預覽）
- 工具權限回呼，含「一律允許」（回傳 CLI 給的建議權限）
- 權限模式：開對話時指定（`--permission-mode`）、對話中途換（`set_permission_mode`），以及交給 MCP 工具回答（`--permission-prompt-tool`）或沒有人回答（`--permission-prompts none`）
- 可以用的工具、不經詢問允許的工具、停用的工具（`--tools`、`--allowedTools`、`--disallowedTools`），以及工具名稱的別名
- 外部 MCP server（`--mcp-config`、`--strict-mcp-config`），對話中途查狀態、開關、重連、整組替換、讀 UI 資源
- 自訂 subagent 與主對話的 agent（initialize 的 `agents`、`--agent`）、背景任務的訊息、停止與移到背景（`stop_task`、`background_tasks`）
- 設定：額外的設定與沙箱（`--settings`）、管理設定層（`--managed-settings`）、要載入哪些設定檔（`--setting-sources`）
- 外掛與 skill：載入本機外掛（`--plugin-dir`，或放在 initialize 裡送）、篩選 skill、對話中途重新載入
- 部分訊息串流（`--include-partial-messages`）
- 中斷這一輪
- 系統提示詞：Claude Code 預設的、在預設後面附加、自訂全文、從檔案讀
- 指定模型與思考深度（`--model`、`--effort`），對話中途換模型（`set_model`）
- 對話中途套用設定，例如換思考深度（`apply_flag_settings`；Python SDK 沒有，照 TypeScript SDK 的協定）
- 輪數與費用上限（`--max-turns`、`--max-budget-usd`）
- 結構化輸出（`--json-schema`）
- 額外的 CLI 參數，給還沒有專屬選項的旗標用（`extra_args`）
- 查 context window 用量（`get_context_usage`），可以用 `detail: summary` 快速估算（Python SDK 沒有，照 TypeScript SDK 的協定）

還沒做，之後會補：hook 回呼、讀寫 `~/.claude/projects` 的 session 函式（列出、讀取、改名、刪除等）、SDK 內的 MCP server（自訂工具；現在可以自己開 MCP server，用 `McpServers` 接上）。

不做：session store、transcript mirror。

## 需求

- .NET 10
- 已安裝 Claude Code CLI。Windows 請用原生安裝的 `claude.exe`，不支援 npm 的 `claude.cmd`（批次檔會讓 cmd.exe 重新解析參數，有指令注入風險）。
- CLI 最低版本 2.0.0，跟 Python SDK 一樣。指定 `WarningCallback` 時，連線前會執行 `claude -v`，版本太舊就透過它警告，但不會中斷連線；環境變數 `CLAUDE_AGENT_SDK_SKIP_VERSION_CHECK` 設成任何值就不檢查。

## 授權

MIT，見 [LICENSE](LICENSE)；移植來源的授權聲明見 [NOTICE](NOTICE)。
