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
| Claude Code CLI | 2.1.312 (bundled with Python SDK v0.2.174) |
| CLI used to record protocol fixtures | 2.1.309 |

## Compared with the official SDKs

- Covers Python's `ClaudeSDKClient`: multi-turn conversations, tool approval, interrupts, and resuming sessions; there is no one-shot `query()`.
- The 13 options most used in conversations are supported (Python has 49, TypeScript 69); permission modes, allowed-tool lists, turn limits, MCP, and subagents are not available yet, and what is not planned is listed under [Scope](#scope).
- Beyond the official SDKs: `ApplyFlagSettingsAsync` (missing from Python), `SetModelAsync` returning the full model ID, `WarningCallback`, `Raw` on every message with unrecognized messages kept, refusing `.cmd` on Windows, and AOT compatibility.
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
```

- Without `SystemPrompt`, an empty system prompt is sent, as in the Python SDK, so Claude does not get Claude Code's default one (coding guidelines, working directory, git status, and so on); use `SystemPromptPreset` to match the terminal experience.
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

Not planned: hook callbacks, in-SDK MCP servers, session stores, transcript mirroring.

## Requirements

- .NET 10
- Claude Code CLI installed. On Windows, use the native `claude.exe`; the npm `claude.cmd` wrapper is not supported, because cmd.exe re-parses the arguments of batch files, which allows command injection.
- The minimum CLI version is 2.0.0, as in the Python SDK. With `WarningCallback` set, the SDK runs `claude -v` before connecting and warns through it when the CLI is older, without failing the connection; set `CLAUDE_AGENT_SDK_SKIP_VERSION_CHECK` to any value to skip the check.

## License

MIT, see [LICENSE](https://github.com/pamis-wang/claude-agent-sdk-dotnet/blob/main/LICENSE). The license notice of the ported source is in [NOTICE](https://github.com/pamis-wang/claude-agent-sdk-dotnet/blob/main/NOTICE).
