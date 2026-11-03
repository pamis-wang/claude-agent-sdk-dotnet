# claude-agent-sdk-dotnet compared with the official Python and TypeScript SDKs

English | [繁體中文](comparison-with-official-sdks.zh-TW.md)

How `PamisWang.ClaudeAgentSdk` compares with Anthropic's official Claude Agent SDK for Python and TypeScript: what the port covers, what it leaves out on purpose, and what it adds.

- Surveyed: 2026-11-03
- Official Python: claude-agent-sdk 0.2.177 (8abce11)
- Official TypeScript: @anthropic-ai/claude-agent-sdk 0.3.317
- .NET: PamisWang.ClaudeAgentSdk 0.1.0-preview.7, plus the unreleased `MaxTurns`, `MaxBudgetUsd`, and `OutputSchema`
- The .NET package tracks Python 0.2.174; 0.2.175 and 0.2.176 only update the bundled Claude CLI (2.1.314, 2.1.316), and 0.2.177 bundles CLI 2.1.317 and adds `HookMatcher.on_failure`, part of the hooks the .NET package leaves out on purpose. None of the APIs the .NET package uses changed, so features are compared against the latest release. TypeScript columns are based on the `sdk.d.ts` types in the package.

Related: [Claude .NET vs Codex .NET](claude-vs-codex-dotnet.md). The [comparison of Codex .NET with the official SDKs](https://github.com/pamis-wang/codex-agent-sdk-dotnet/blob/main/docs/comparison-with-official-sdks.md) is in the codex-agent-sdk-dotnet repo.

## Key points

1. **A port of the interactive client**: The .NET package covers Python's `ClaudeSDKClient` (connect, send, receive, approve tools, interrupt, resume sessions); there is no one-shot `query()` function and no public custom transport.
2. **About a quarter of the options**: Python's `ClaudeAgentOptions` has 49 fields and TypeScript's `Options` has 69; the .NET `ClaudeAgentOptions` has 17, focused on what conversations use most. CLI flags without a dedicated option can be passed through `ExtraArguments`.
3. **Four areas left out on purpose**: Hook callbacks, in-SDK MCP servers (custom tools), session stores, and transcript mirroring. The README lists them as not planned.
4. **What it adds**: `ApplyFlagSettingsAsync` (missing from Python, follows the TypeScript protocol), `SetModelAsync` returning the full model ID, `WarningCallback`, `Raw` on every message with unrecognized messages kept (Python drops them), refusing `.cmd` on Windows, and AOT compatibility.
5. **Thorough tests**: 210 tests, including protocol fixtures recorded from the real CLI (`Fixtures/*.jsonl`) and tests against a fake CLI subprocess.
6. **Licensing**: Python is MIT and can be ported; TypeScript is proprietary (© Anthropic), so the .NET package only follows the protocol messages it sends and ports none of its code.

## Feature matrix

Legend: ✅ Supported, ⭐ Extra, 🟡 Partial, ❌ No, — N/A (⭐ marks what goes beyond the official SDKs)

A ❌ for a feature that is only a CLI flag (such as `--add-dir` or `--permission-mode`) means .NET has no dedicated option for it; the flag can still be passed through `ExtraArguments`.

### Entry points and connection

| Feature | Official Python (claude-agent-sdk 0.2.177) | Official TypeScript (claude-agent-sdk 0.3.317) | .NET (PamisWang.ClaudeAgentSdk) |
|---|---|---|---|
| **One-shot query** | ✅ `query(prompt, options)`; the prompt can be a string or an async stream | ✅ `query({ prompt, options })` | ❌ use `ClaudeAgentClient` instead |
| **Interactive client** | ✅ `ClaudeSDKClient`: `connect`, `query`, `receive_response`, `disconnect` | 🟡 the `Query` object returned by `query()`, plus `streamInput` | ✅ `ClaudeAgentClient`: `ConnectAsync`, `SendAsync`, `ReceiveResponseAsync`, `DisposeAsync` |
| **Custom transport** | ✅ public `Transport` abstract class | 🟡 `spawnClaudeCodeProcess` for custom launching | ❌ `ITransport` is internal |
| **CLI path** | ✅ `cli_path` | ✅ `pathToClaudeCodeExecutable`, `executable`, `executableArgs` | ⭐ `CliPath`; refuses npm's `claude.cmd` on Windows (command injection risk) |
| **Bundled CLI** | ✅ Claude CLI bundled in the package (`_bundled`) | ✅ bundled | ❌ install the native `claude.exe` yourself |
| **Version check** | 🟡 `_check_claude_version`, warns through `logging` | — not visible in the types | ⭐ `WarningCallback`; skip with `CLAUDE_AGENT_SDK_SKIP_VERSION_CHECK` |
| **Environment variables** | ✅ `env` | ✅ `env` | ✅ `EnvironmentVariables` |
| **Extra CLI arguments** | ✅ `extra_args` | ✅ `extraArgs` | ⭐ `ExtraArguments`; also rejects malformed flag names and the flags the SDK controls |
| **Working directory** | ✅ `cwd`, `add_dirs` | ✅ `cwd`, `additionalDirectories` | 🟡 `WorkingDirectory` (no extra directories) |
| **stderr** | ✅ `stderr` | ✅ `stderr` | ✅ `StandardErrorCallback` |
| **Message size limit** | ✅ `max_buffer_size` | — | ✅ `MaxBufferSize` |

### Conversations and sessions

| Feature | Official Python (claude-agent-sdk 0.2.177) | Official TypeScript (claude-agent-sdk 0.3.317) | .NET (PamisWang.ClaudeAgentSdk) |
|---|---|---|---|
| **Resume a session** | ✅ `resume`, `continue_conversation`, `session_id`, `resume_session_at` | ✅ `resume`, `continue`, `sessionId`, `resumeSessionAt` | 🟡 `ResumeSessionId` |
| **Fork a session** | ✅ `fork_session` option and function | ✅ `forkSession` | ❌ |
| **List / read history** | ✅ `list_sessions`, `get_session_info`, `get_session_messages`, `list_subagents` | ✅ `listSessions`, `getSessionMessages`, and more | ❌ |
| **Rename / tag / delete** | ✅ `rename_session`, `tag_session`, `delete_session` | ✅ `renameSession`, `tagSession`, `deleteSession` | ❌ |
| **Session store / transcript mirror** | ✅ `session_store`, `InMemorySessionStore` | ✅ `sessionStore` | ❌ not planned |
| **Interrupt** | ✅ `interrupt()` | ✅ `interrupt()` | ✅ `InterruptAsync`; pending permission prompts get their token cancelled |
| **Partial message streaming** | ✅ `include_partial_messages` | ✅ `includePartialMessages` | ⭐ `IncludePartialMessages`, plus `StreamEvent.TextDelta` for the text |
| **Turn / budget limits** | ✅ `max_turns`, `max_budget_usd`, `task_budget` | ✅ `maxTurns`, `maxBudgetUsd`, `taskBudget` | 🟡 `MaxTurns`, `MaxBudgetUsd` (a `decimal` amount); no task budget |
| **Structured output** | ✅ `output_format` | ✅ `outputFormat` | ✅ `OutputSchema` (a `JsonObject`); the result is in `ResultMessage.StructuredOutput` |
| **File checkpoints / rewind** | ✅ `enable_file_checkpointing`, `rewind_files` | ✅ `enableFileCheckpointing`, `rewindFiles` | ❌ |

### Models and prompts

| Feature | Official Python (claude-agent-sdk 0.2.177) | Official TypeScript (claude-agent-sdk 0.3.317) | .NET (PamisWang.ClaudeAgentSdk) |
|---|---|---|---|
| **System prompt** | ✅ string, `SystemPromptPreset`, `SystemPromptCustom`, `SystemPromptFile` | ✅ `systemPrompt` | ✅ `SystemPromptPreset`, `SystemPromptCustom`, `SystemPromptFile` |
| **Model** | ✅ `model`, `fallback_model` | ✅ `model`, `fallbackModel` | 🟡 `Model` (no fallback model) |
| **Switch model mid-conversation** | ✅ `set_model()` | ✅ `setModel()` | ⭐ `SetModelAsync`, also returning the full model ID the CLI switched to |
| **Effort / thinking** | ✅ `effort`, `thinking`, `max_thinking_tokens` | ✅ `effort`, `thinking`, `setMaxThinkingTokens()` | 🟡 `Effort` (no thinking settings) |
| **Apply settings mid-conversation** | ❌ | ✅ `applyFlagSettings()`, `updateSettings()` | ⭐ `ApplyFlagSettingsAsync`, following the protocol TypeScript sends |
| **Betas** | ✅ `betas` | ✅ `betas` | ❌ |
| **Available models / commands** | 🟡 `get_server_info()` | ✅ `supportedModels()`, `supportedCommands()`, `supportedAgents()` | 🟡 `ServerInfo` (raw JSON) |

### Tools, permissions, and extensions

| Feature | Official Python (claude-agent-sdk 0.2.177) | Official TypeScript (claude-agent-sdk 0.3.317) | .NET (PamisWang.ClaudeAgentSdk) |
|---|---|---|---|
| **Tool permission callback** | ✅ `can_use_tool` | ✅ `canUseTool` | ✅ `CanUseTool`; return `request.Suggestions` to always allow |
| **Allowed / disallowed tools** | ✅ `tools`, `allowed_tools`, `disallowed_tools` | ✅ `tools`, `allowedTools`, `disallowedTools`, `toolAliases` | 🟡 `DisallowedTools` |
| **Permission modes** | ✅ `permission_mode`, `set_permission_mode()` | ✅ `permissionMode`, `setPermissionMode()` | ❌ |
| **Permission prompt tool** | ✅ `permission_prompt_tool_name` | ✅ `permissionPromptToolName` | ❌ |
| **Hook callbacks** | ✅ `hooks` (`HookMatcher`, more than twenty events; `on_failure` decides whether a failed callback blocks the action) | ✅ `hooks` (matchers take `onFailure`) | ❌ not planned |
| **In-SDK MCP servers** | ✅ `create_sdk_mcp_server`, `@tool` | ✅ `createSdkMcpServer`, `tool` | ❌ not planned |
| **External MCP servers** | ✅ `mcp_servers`, `strict_mcp_config` | ✅ `mcpServers`, `setMcpServers()` | ❌ |
| **MCP status / reconnect / toggle** | ✅ `get_mcp_status`, `reconnect_mcp_server`, `toggle_mcp_server` | ✅ `mcpServerStatus()`, `reconnectMcpServer()`, `toggleMcpServer()`, `readMcpResource()` | ❌ |
| **Subagent definitions** | ✅ `agents` (`AgentDefinition`) | ✅ `agents`, `agent` | ❌ |
| **Background tasks** | ✅ `stop_task()`, `Task*Message` | ✅ `stopTask()`, `backgroundTasks()` | 🟡 arrive as `SystemMessage`; read the details from `Raw` |
| **Sandbox settings** | ✅ `sandbox` | ✅ `sandbox` | ❌ |
| **Setting sources / plugins / skills** | ✅ `setting_sources`, `settings`, `plugins`, `skills` | ✅ `settingSources`, `managedSettings`, `plugins`, `reloadPlugins()` | ❌ |
| **Context usage** | ✅ `get_context_usage()` | ✅ `getContextUsage()` | ❌ |

### Messages and errors

| Feature | Official Python (claude-agent-sdk 0.2.177) | Official TypeScript (claude-agent-sdk 0.3.317) | .NET (PamisWang.ClaudeAgentSdk) |
|---|---|---|---|
| **Main message types** | ✅ User, Assistant, System, Result, StreamEvent, RateLimitEvent, and more | ✅ 40 `SDKMessage` variants | ✅ `UserMessage`, `AssistantMessage`, `SystemMessage`, `ResultMessage`, `StreamEvent`, `RateLimitEvent`, `ConversationResetMessage` |
| **Content blocks** | ✅ Text, Thinking, ToolUse, ToolResult, ServerToolUse, ServerToolResult | ✅ | ✅ same as Python, plus `UnknownContentBlock` |
| **Unrecognized messages** | 🟡 dropped (debug log only) | — not visible in the types | ⭐ `UnknownMessage`, never dropped |
| **Raw JSON kept** | ❌ | ❌ | ⭐ `Raw` on every message and content block |
| **Exception types** | ✅ `ClaudeSDKError`, `CLIConnectionError`, `CLINotFoundError`, `ProcessError`, `CLIJSONDecodeError` | 🟡 `AbortError` | ✅ `ClaudeAgentException`, `CliConnectionException`, `CliNotFoundException`, `CliProcessException`, `CliJsonDecodeException`, `MessageParseException` |

## Size and quality

| Item | Official Python | Official TypeScript | .NET |
|---|---|---|---|
| Code | about 13,500 lines of Python | shipped as a bundle; the `sdk.d.ts` types are about 10,500 lines | about 2,690 hand-written lines of C# (48 files) |
| Public client methods | 15 on `ClaudeSDKClient` | 31 on `Query` | 7 async methods on `ClaudeAgentClient` |
| Options | 49 | 69 | 17 |
| Tests | pytest | not published | 210 (xUnit), including fixtures recorded from the real CLI and a fake CLI subprocess |
| License | MIT | proprietary (© Anthropic) | MIT, with the Python SDK's notice kept in NOTICE |
| AOT | — | — | `IsAotCompatible` |

## Suggested next steps

- Turn the most requested CLI flags into typed options: `AllowedTools`, `PermissionMode`, and `AddDirectories`. All of them can already go through `ExtraArguments`; dedicated options add type checking and documentation. `SetPermissionModeAsync` is a control request, not a flag, so it still needs its own method.
- `ForkSession` and `ContinueConversation` are just CLI arguments and quick to add; the session-listing functions need to read `~/.claude/projects` and take more work.
- Background task messages such as `TaskStarted` can only be read from `SystemMessage.Raw` today; they could get dedicated types.
- Keep hooks and in-SDK MCP servers out unless there is a clear need; they require the two-way control protocol and a JSON-RPC bridge, the most complex parts.

