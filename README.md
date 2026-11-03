# PamisWang.ClaudeAgentSdk

English | [繁體中文](https://github.com/pamis-wang/claude-agent-sdk-dotnet/blob/main/README.zh-TW.md)

A .NET client for Claude Code: it launches the local `claude` CLI and talks to it over the stream-json protocol to chat, approve tool use, interrupt, and resume sessions.

> **Unofficial.** Ported from [claude-agent-sdk-python](https://github.com/anthropics/claude-agent-sdk-python) and not affiliated with Anthropic. The official Agent SDK is available only for Python and TypeScript.
> Features that only the TypeScript SDK has are written against the protocol messages it sends to the CLI and verified against the real CLI; the TypeScript SDK is not open-source licensed, and none of its code is ported.

## Status

A preview is available on NuGet; the API may still change before 1.0.

```bash
dotnet add package PamisWang.ClaudeAgentSdk --prerelease
```

| Tracks | Version |
|---|---|
| Python SDK | v0.2.174 |
| TypeScript SDK | v0.3.311 (for features Python lacks, following the protocol messages it sends) |
| Claude Code CLI | 2.1.310 (bundled with Python SDK v0.2.174) |
| CLI used to record protocol fixtures | 2.1.309; 2.1.312 for the permission mode, subagent, MCP, and rewind fixtures |

## Compared with the official SDKs

- Covers Python's `ClaudeSDKClient`: multi-turn conversations, tool approval, interrupts, and resuming sessions; there is no one-shot `query()`.
- 50 options (Python has 49, TypeScript 69, split differently): for conversations and sessions, and for tools, permissions, and extensions, everything Python and TypeScript offer is covered, including TypeScript-only options such as `ToolAliases`, `ManagedSettings`, and `PerTaskStopAffordance`. CLI flags without a dedicated option can still be passed through `ExtraArguments`. What is still missing is listed under [Scope](#scope).
- Beyond the official SDKs: `ApplyFlagSettingsAsync` (missing from Python), `SetModelAsync` and `SetPermissionModeAsync` returning the value the CLI actually switched to, the `detail` option of `GetContextUsageAsync` (missing from Python), `RewindFilesAsync` returning a result and accepting `dryRun` (missing from Python), `WarningCallback`, `Raw` on every message with unrecognized messages kept, conflicting options rejected before connecting, refusing `.cmd` on Windows, and AOT compatibility.
- Full comparisons: [with the official Python and TypeScript SDKs](https://github.com/pamis-wang/claude-agent-sdk-dotnet/blob/main/docs/comparison-with-official-sdks.md), [with PamisWang.CodexAgentSdk](https://github.com/pamis-wang/claude-agent-sdk-dotnet/blob/main/docs/claude-vs-codex-dotnet.md)

## Usage

```csharp
using System.Text.Json.Nodes;
using PamisWang.ClaudeAgentSdk;
using PamisWang.ClaudeAgentSdk.Mcp;
using PamisWang.ClaudeAgentSdk.Messages;
using PamisWang.ClaudeAgentSdk.Permissions;

var options = new ClaudeAgentOptions
{
    WorkingDirectory = @"D:\work\my-project",
    SystemPrompt = new SystemPromptPreset(), // Claude Code's default system prompt; empty if omitted
    Model = "sonnet",
    Effort = "high",
    ResumeSessionId = null,               // a session ID to resume
    IncludePartialMessages = true,        // receive output as it streams
    DisallowedTools = ["AskUserQuestion"],
    AllowedTools = ["Read", "Grep", "Bash(git status)"], // allowed without asking
    PermissionMode = PermissionMode.Default,  // always approve through CanUseTool; omitted, the CLI decides from settings
    MaxTurns = 20,                        // limit on model round trips
    MaxBudgetUsd = 1.00m,                 // spending limit in USD
    McpServers = new Dictionary<string, McpServerConfig>
    {
        ["docs"] = new McpHttpServerConfig("http://127.0.0.1:5000/mcp"),
    },
    // CLI flags without a dedicated option; null means a flag without a value
    ExtraArguments = new Dictionary<string, string?> { ["add-dir"] = @"D:\work\shared" },
    // asked before a tool runs; cancelled on interrupt
    CanUseTool = async (request, cancellationToken) =>
        await AskUserAsync(request.ToolName, request.Input, cancellationToken)
            ? new PermissionResultAllow(UpdatedPermissions: request.Suggestions) // always allow
            : new PermissionResultDeny("denied by the user"),
};

await using var client = new ClaudeAgentClient(options);
await client.ConnectAsync();
await client.SendAsync("What does this project do?");

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
        case TaskStartedMessage task:             // a subagent or background command started; StopTaskAsync stops it
            Console.WriteLine($"\n[task {task.TaskId}] {task.Description}");
            break;
        case ResultMessage result:
            Console.WriteLine($"\n{result.Subtype}, {result.DurationMilliseconds} ms");
            break;
    }
}

// keep talking, or interrupt the current turn
await client.SendAsync("How do I run the tests?");
await client.InterruptAsync();

// switch the model, effort, or permission mode mid-conversation
await client.SetModelAsync("opus");
await client.ApplyFlagSettingsAsync(new JsonObject { ["effortLevel"] = "max" });
await client.SetPermissionModeAsync(PermissionMode.AcceptEdits);

// MCP server connection status
foreach (var server in await client.GetMcpStatusAsync())
{
    Console.WriteLine($"{server.Name}: {server.Status}");
}

// how much of the context window is used, broken down like the /context command
var usage = await client.GetContextUsageAsync();
Console.WriteLine($"{usage.Percentage}% of {usage.RawMaxTokens} tokens");
```

- Without `SystemPrompt`, an empty system prompt is sent, as in the Python SDK, so Claude does not get Claude Code's default one (coding guidelines, working directory, git status, and so on); use `SystemPromptPreset` to match the terminal experience.
- `MaxTurns` counts each message you send separately; going over it ends the turn with `ResultMessage.Subtype` set to `error_max_turns`. `MaxBudgetUsd` covers the whole session (like `TotalCostUsd`) and is checked after each API call, so spending can go slightly over; going over it ends the turn with `error_max_budget_usd`, and later messages end the same way. Both must be greater than 0.
- Structured output: give `OutputSchema` a JSON Schema (a `JsonObject`); the result is in `ResultMessage.StructuredOutput`, and `Deserialize<T>()` turns it into your own type. Claude Code has the model call a tool to hand in the result, which takes a few extra round trips, so do not set `MaxTurns` too low.
- `GetContextUsageAsync` counts each category with the token-count API by default, which takes a moment; `ContextUsageDetail.Summary` answers almost instantly with estimates. Use `Id` to find a category and `Kind` (`used`, `free`, `buffer`, `deferred`) to classify it, not the English `Name`. Fields without a property, such as `messageBreakdown` and `apiUsage`, are in `Raw`.
- Without `PermissionMode`, the CLI decides: a `permissions.defaultMode` in the settings it loads, otherwise `Auto` where auto mode is available. To approve every action through `CanUseTool`, set `PermissionMode.Default` explicitly. `PermissionMode` wraps a string, so a mode the CLI adds later can be passed as `new PermissionMode("...")`.
- `Tools` limits which built-in tools the model sees (an empty list disables them all), `AllowedTools` lists tools allowed without asking, and `DisallowedTools` removes tools; they can be combined.
- Conflicting options make `ConnectAsync` throw `ArgumentException`, for example `ContinueConversation` together with `ResumeSessionId`, `ForkSession` without a session to resume, or both `CanUseTool` and `PermissionPromptToolName`.
- `ExtraArguments` keys are flag names without the leading `--`. A value starting with `-` is sent as `--flag=value` so the CLI does not read it as another flag. `output-format` and `input-format` are controlled by this package and rejected, and so is `permission-prompt-tool` when `CanUseTool` or `PermissionPromptToolName` is set.
- The available models and the effort levels each supports are listed under `models` in `ServerInfo`.
- `ReceiveResponseAsync` stops after the turn's `ResultMessage`; `ReceiveMessagesAsync` keeps reading across turns, which suits a background receive loop.
- Every message carries `Raw` (the original JSON) for fields without a property; unrecognized message types arrive as `UnknownMessage` instead of being dropped.
- A client connects once; to start over, create a new client and use `ResumeSessionId` to continue the session.

## Scope

Supported:

- Multi-turn conversations (stream-json input), optionally sent verbatim without expanding `@file` mentions or slash commands (`VerbatimPrompts`)
- Resuming sessions: a given one (`--resume`), the most recent (`--continue`), forked into a new session (`--fork-session`), with a chosen ID for the new session (`--session-id`), or only up to a given message (`--resume-session-at`, checked with `--resume-drops-turn`); sessions can also be left unsaved (`--no-session-persistence`)
- File checkpoints and rewinding (`rewind_files`, with an optional preview)
- Tool permission callback, including "always allow" (returning the CLI's suggested permissions)
- Permission modes: at launch (`--permission-mode`), switched mid-conversation (`set_permission_mode`), answered by an MCP tool (`--permission-prompt-tool`), or answered by nobody (`--permission-prompts none`)
- Available tools, tools allowed without asking, and disallowed tools (`--tools`, `--allowedTools`, `--disallowedTools`), plus tool-name aliases
- External MCP servers (`--mcp-config`, `--strict-mcp-config`), with status, toggling, reconnecting, replacing the set, and reading UI resources mid-conversation
- Custom subagents and the main-thread agent (`agents` in initialize, `--agent`), background task messages, and stopping or backgrounding tasks (`stop_task`, `background_tasks`)
- Settings: extra settings and the sandbox (`--settings`), a managed settings tier (`--managed-settings`), and which settings files to load (`--setting-sources`)
- Plugins and skills: loading local plugins (`--plugin-dir`, or sent in initialize), filtering skills, and reloading both mid-conversation
- Partial message streaming (`--include-partial-messages`)
- Interrupting the current turn
- System prompts: Claude Code's default, appended to the default, fully custom, or read from a file
- Choosing the model and effort (`--model`, `--effort`), and switching models mid-conversation (`set_model`)
- Applying settings mid-conversation, such as the effort level (`apply_flag_settings`; missing from the Python SDK, follows the TypeScript SDK's protocol)
- Turn and spending limits (`--max-turns`, `--max-budget-usd`)
- Structured output (`--json-schema`)
- Extra CLI arguments for flags without a dedicated option (`extra_args`)
- Context window usage (`get_context_usage`), with a quick estimate via `detail: summary` (missing from the Python SDK, follows the TypeScript SDK's protocol)

Not yet, planned: hook callbacks, session functions that read and write `~/.claude/projects` (listing, reading, renaming, deleting, and so on), and in-SDK MCP servers (custom tools; for now, host your own MCP server and connect it through `McpServers`).

Not planned: session stores, transcript mirroring.

## Requirements

- .NET 10
- Claude Code CLI installed. On Windows, use the native `claude.exe`; the npm `claude.cmd` wrapper is not supported, because cmd.exe re-parses the arguments of batch files, which allows command injection.
- The minimum CLI version is 2.0.0, as in the Python SDK. With `WarningCallback` set, the SDK runs `claude -v` before connecting and warns through it when the CLI is older, without failing the connection; set `CLAUDE_AGENT_SDK_SKIP_VERSION_CHECK` to any value to skip the check.

## License

MIT, see [LICENSE](https://github.com/pamis-wang/claude-agent-sdk-dotnet/blob/main/LICENSE). The license notice of the ported source is in [NOTICE](https://github.com/pamis-wang/claude-agent-sdk-dotnet/blob/main/NOTICE).
