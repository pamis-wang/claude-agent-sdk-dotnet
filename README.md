# PamisWang.ClaudeAgentSdk

English | [繁體中文](https://github.com/pamis-wang/claude-agent-sdk-dotnet/blob/main/README.zh-TW.md)

A .NET client for Claude Code: it launches the local `claude` CLI and talks to it over the stream-json protocol to chat, approve tool use, interrupt, and resume sessions.

> **Unofficial.** Ported from [claude-agent-sdk-python](https://github.com/anthropics/claude-agent-sdk-python) and not affiliated with Anthropic. The official Agent SDK is available only for Python and TypeScript.
> The few features missing from the Python SDK are written against the protocol messages the TypeScript SDK sends to the CLI; the TypeScript SDK is not open-source licensed, and none of its code is ported.

## Status

A preview is available on NuGet; the API may still change before 1.0.

```bash
dotnet add package PamisWang.ClaudeAgentSdk --prerelease
```

| Tracks | Version |
|---|---|
| Python SDK | v0.2.174 |
| TypeScript SDK | v0.3.316 (only for `apply_flag_settings`, which Python lacks) |
| Claude Code CLI | 2.1.310 (bundled with Python SDK v0.2.174) |
| CLI used to record protocol fixtures | 2.1.309 |

## Compared with the official SDKs

- Covers Python's `ClaudeSDKClient`: multi-turn conversations, tool approval, interrupts, and resuming sessions; there is no one-shot `query()`.
- The 17 options most used in conversations are supported (Python has 49, TypeScript 69); permission modes, allowed-tool lists, MCP, and subagents have no dedicated options yet, but anything that is just a CLI flag can be passed through `ExtraArguments`. What is not planned is listed under [Scope](#scope).
- Beyond the official SDKs: `ApplyFlagSettingsAsync` (missing from Python), `SetModelAsync` returning the full model ID, the `detail` option of `GetContextUsageAsync` (missing from Python), `WarningCallback`, `Raw` on every message with unrecognized messages kept, refusing `.cmd` on Windows, and AOT compatibility.
- Full comparisons: [with the official Python and TypeScript SDKs](https://github.com/pamis-wang/claude-agent-sdk-dotnet/blob/main/docs/comparison-with-official-sdks.md), [with PamisWang.CodexAgentSdk](https://github.com/pamis-wang/claude-agent-sdk-dotnet/blob/main/docs/claude-vs-codex-dotnet.md)

## Usage

```csharp
using System.Text.Json.Nodes;
using PamisWang.ClaudeAgentSdk;
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
    MaxTurns = 20,                        // limit on model round trips
    MaxBudgetUsd = 1.00m,                 // spending limit in USD
    // CLI flags without a dedicated option; null means a flag without a value
    ExtraArguments = new Dictionary<string, string?> { ["add-dir"] = @"D:\work\shared", ["strict-mcp-config"] = null },
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
        case ResultMessage result:
            Console.WriteLine($"\n{result.Subtype}, {result.DurationMilliseconds} ms");
            break;
    }
}

// keep talking, or interrupt the current turn
await client.SendAsync("How do I run the tests?");
await client.InterruptAsync();

// switch the model or effort mid-conversation
await client.SetModelAsync("opus");
await client.ApplyFlagSettingsAsync(new JsonObject { ["effortLevel"] = "max" });

// how much of the context window is used, broken down like the /context command
var usage = await client.GetContextUsageAsync();
Console.WriteLine($"{usage.Percentage}% of {usage.RawMaxTokens} tokens");
```

- Without `SystemPrompt`, an empty system prompt is sent, as in the Python SDK, so Claude does not get Claude Code's default one (coding guidelines, working directory, git status, and so on); use `SystemPromptPreset` to match the terminal experience.
- `MaxTurns` counts each message you send separately; going over it ends the turn with `ResultMessage.Subtype` set to `error_max_turns`. `MaxBudgetUsd` covers the whole session (like `TotalCostUsd`) and is checked after each API call, so spending can go slightly over; going over it ends the turn with `error_max_budget_usd`, and later messages end the same way. Both must be greater than 0.
- Structured output: give `OutputSchema` a JSON Schema (a `JsonObject`); the result is in `ResultMessage.StructuredOutput`, and `Deserialize<T>()` turns it into your own type. Claude Code has the model call a tool to hand in the result, which takes a few extra round trips, so do not set `MaxTurns` too low.
- `GetContextUsageAsync` counts each category with the token-count API by default, which takes a moment; `ContextUsageDetail.Summary` answers almost instantly with estimates. Use `Id` to find a category and `Kind` (`used`, `free`, `buffer`, `deferred`) to classify it, not the English `Name`. Fields without a property, such as `messageBreakdown` and `apiUsage`, are in `Raw`.
- `ExtraArguments` keys are flag names without the leading `--`. A value starting with `-` is sent as `--flag=value` so the CLI does not read it as another flag. `output-format` and `input-format` are controlled by this package and rejected, and so is `permission-prompt-tool` when `CanUseTool` is set.
- The available models and the effort levels each supports are listed under `models` in `ServerInfo`.
- `ReceiveResponseAsync` stops after the turn's `ResultMessage`; `ReceiveMessagesAsync` keeps reading across turns, which suits a background receive loop.
- Every message carries `Raw` (the original JSON) for fields without a property; unrecognized message types arrive as `UnknownMessage` instead of being dropped.
- A client connects once; to start over, create a new client and use `ResumeSessionId` to continue the session.

## Scope

Supported:

- Multi-turn conversations (stream-json input)
- Resuming sessions (`--resume`)
- Tool permission callback, including "always allow" (returning the CLI's suggested permissions)
- Partial message streaming (`--include-partial-messages`)
- Disallowing specific tools (`--disallowedTools`)
- Interrupting the current turn
- System prompts: Claude Code's default, appended to the default, fully custom, or read from a file
- Choosing the model and effort (`--model`, `--effort`), and switching models mid-conversation (`set_model`)
- Applying settings mid-conversation, such as the effort level (`apply_flag_settings`; missing from the Python SDK, follows the TypeScript SDK's protocol)
- Turn and spending limits (`--max-turns`, `--max-budget-usd`)
- Structured output (`--json-schema`)
- Extra CLI arguments for flags without a dedicated option (`extra_args`)
- Context window usage (`get_context_usage`), with a quick estimate via `detail: summary` (missing from the Python SDK, follows the TypeScript SDK's protocol)

Not planned: hook callbacks, in-SDK MCP servers, session stores, transcript mirroring.

## Requirements

- .NET 10
- Claude Code CLI installed. On Windows, use the native `claude.exe`; the npm `claude.cmd` wrapper is not supported, because cmd.exe re-parses the arguments of batch files, which allows command injection.
- The minimum CLI version is 2.0.0, as in the Python SDK. With `WarningCallback` set, the SDK runs `claude -v` before connecting and warns through it when the CLI is older, without failing the connection; set `CLAUDE_AGENT_SDK_SKIP_VERSION_CHECK` to any value to skip the check.

## License

MIT, see [LICENSE](https://github.com/pamis-wang/claude-agent-sdk-dotnet/blob/main/LICENSE). The license notice of the ported source is in [NOTICE](https://github.com/pamis-wang/claude-agent-sdk-dotnet/blob/main/NOTICE).
