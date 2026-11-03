# PamisWang.ClaudeAgentSdk

[English](README.md) | 繁體中文

Claude Code 的 .NET 用戶端：啟動本機的 `claude` CLI，用 stream-json 協定對話、批准工具、中斷、接續 session。

> **非官方**。移植自 [claude-agent-sdk-python](https://github.com/anthropics/claude-agent-sdk-python)，與 Anthropic 無關。官方的 Agent SDK 只有 Python 與 TypeScript。
> Python SDK 沒有的少數功能，照 TypeScript SDK 送給 CLI 的協定格式另外寫；TypeScript SDK 不是開源授權，沒有移植它的程式碼。

## 狀態

預覽版已經發佈到 NuGet；1.0 之前 API 可能還會變。

```bash
dotnet add package PamisWang.ClaudeAgentSdk --prerelease
```

| 對照 | 版本 |
|---|---|
| Python SDK | v0.2.174 |
| TypeScript SDK | v0.3.316（只參考 Python 沒有的 `apply_flag_settings`） |
| Claude Code CLI | 2.1.310（Python SDK v0.2.174 打包的版本） |
| 錄協定樣本用的 CLI | 2.1.309 |

## 跟官方 SDK 比較

- 對應 Python 的 `ClaudeSDKClient`：多輪對話、批准工具、中斷、接續 session；沒有一次性的 `query()`。
- 支援對話最常用的 13 個選項（Python 有 49 個、TypeScript 有 69 個）；權限模式、允許的工具清單、輪數上限、MCP、subagent 等還沒有，刻意不做的見下方的[範圍](#範圍)。
- 比官方多：`ApplyFlagSettingsAsync`（Python 沒有）、`SetModelAsync` 回傳完整的模型 ID、`WarningCallback`、每則訊息都有 `Raw` 而且不丟掉不認得的訊息、Windows 拒絕執行 `.cmd`、AOT 相容。
- 完整對照：[與官方 Python、TypeScript SDK](docs/comparison-with-official-sdks.zh-TW.md)、[與 PamisWang.CodexAgentSdk](docs/claude-vs-codex-dotnet.zh-TW.md)

## 使用方式

```csharp
using System.Text.Json.Nodes;
using PamisWang.ClaudeAgentSdk;
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
        case ResultMessage result:
            Console.WriteLine($"\n{result.Subtype}，{result.DurationMilliseconds} ms");
            break;
    }
}

// 接著說下一句，或中斷這一輪
await client.SendAsync("那測試怎麼跑？");
await client.InterruptAsync();

// 對話中途換模型、換思考深度
await client.SetModelAsync("opus");
await client.ApplyFlagSettingsAsync(new JsonObject { ["effortLevel"] = "max" });
```

- 沒指定 `SystemPrompt` 時跟 Python SDK 一樣傳空的系統提示詞，Claude 不會拿到 Claude Code 預設的那份（寫程式的準則、工作目錄與 git 狀態等）；要跟在終端機用時一樣，指定 `SystemPromptPreset`。
- 可以選的模型與各自支援的思考深度，在 `ServerInfo` 的 `models` 裡。
- `ReceiveResponseAsync` 讀到這一輪的 `ResultMessage` 為止；`ReceiveMessagesAsync` 跨越多輪一直讀，適合在背景持續接收。
- 每則訊息都有 `Raw`（原始 JSON），沒有對應屬性的欄位從這裡讀；不認得的訊息型別是 `UnknownMessage`，不會被丟掉。
- 一個 client 只連線一次；要重新開始就建立新的 client，用 `ResumeSessionId` 接續原本的 session。

## 範圍

支援：

- 多輪對話（stream-json 輸入）
- 接續 session（`--resume`）
- 工具權限回呼，含「一律允許」（回傳 CLI 給的建議權限）
- 部分訊息串流（`--include-partial-messages`）
- 停用特定工具（`--disallowedTools`）
- 中斷這一輪
- 系統提示詞：Claude Code 預設的、在預設後面附加、自訂全文、從檔案讀
- 指定模型與思考深度（`--model`、`--effort`），對話中途換模型（`set_model`）
- 對話中途套用設定，例如換思考深度（`apply_flag_settings`；Python SDK 沒有，照 TypeScript SDK 的協定）

不做：hook 回呼、SDK 內的 MCP server、session store、transcript mirror。

## 需求

- .NET 10
- 已安裝 Claude Code CLI。Windows 請用原生安裝的 `claude.exe`，不支援 npm 的 `claude.cmd`（批次檔會讓 cmd.exe 重新解析參數，有指令注入風險）。
- CLI 最低版本 2.0.0，跟 Python SDK 一樣。指定 `WarningCallback` 時，連線前會執行 `claude -v`，版本太舊就透過它警告，但不會中斷連線；環境變數 `CLAUDE_AGENT_SDK_SKIP_VERSION_CHECK` 設成任何值就不檢查。

## 授權

MIT，見 [LICENSE](LICENSE)；移植來源的授權聲明見 [NOTICE](NOTICE)。
