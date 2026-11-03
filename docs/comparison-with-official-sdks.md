# claude-agent-sdk-dotnet compared with the official Python and TypeScript SDKs

English | [繁體中文](comparison-with-official-sdks.zh-TW.md)

How `PamisWang.ClaudeAgentSdk` compares with Anthropic's official Claude Agent SDK for Python and TypeScript: what the port covers, what it leaves out on purpose, and what it adds.

- Surveyed: 2026-11-03
- Official Python: claude-agent-sdk 0.2.177 (8abce11)
- Official TypeScript: @anthropic-ai/claude-agent-sdk 0.3.317
- .NET: PamisWang.ClaudeAgentSdk 0.1.0-preview.9, plus the unreleased completion of conversations and sessions, and tools, permissions, and extensions
- The .NET package tracks Python 0.2.174; 0.2.175 and 0.2.176 only update the bundled Claude CLI (2.1.314, 2.1.316), and 0.2.177 bundles CLI 2.1.317 and adds `HookMatcher.on_failure`, part of the hooks the .NET package does not support yet. None of the APIs the .NET package uses changed, so features are compared against the latest release. TypeScript columns are based on the `sdk.d.ts` types in the package.

Related: [Claude .NET vs Codex .NET](claude-vs-codex-dotnet.md). The [comparison of Codex .NET with the official SDKs](https://github.com/pamis-wang/codex-agent-sdk-dotnet/blob/main/docs/comparison-with-official-sdks.md) is in the codex-agent-sdk-dotnet repo.

## Key points

1. **A port of the interactive client**: The .NET package covers Python's `ClaudeSDKClient` (connect, send, receive, approve tools, interrupt, resume sessions); there is no one-shot `query()` function and no public custom transport.
2. **Conversations and sessions, and tools, permissions, and extensions are complete**: Python's `ClaudeAgentOptions` has 49 fields and TypeScript's `Options` has 69; the .NET `ClaudeAgentOptions` has 50. In these two areas everything Python has is covered, and TypeScript-only features (such as `toolAliases`, `managedSettings`, `perTaskStopAffordance`, `setMcpServers()`, and `reloadPlugins()`) follow the protocol messages TypeScript sends, verified against CLI 2.1.312. CLI flags without a dedicated option can be passed through `ExtraArguments`.
3. **Three areas still to come**: hook callbacks, session functions that read and write `~/.claude/projects`, and in-SDK MCP servers (custom tools). **Left out on purpose**: session stores and transcript mirroring.
4. **What it adds**: `ApplyFlagSettingsAsync` (missing from Python, follows the TypeScript protocol), `SetModelAsync` and `SetPermissionModeAsync` returning the value the CLI actually switched to, the `detail` option of `GetContextUsageAsync`, `RewindFilesAsync` returning a result and accepting `dryRun` (missing from Python), `WarningCallback`, `Raw` on every message with unrecognized messages kept (Python drops them), conflicting options rejected before connecting, refusing `.cmd` on Windows, and AOT compatibility.
5. **Thorough tests**: 325 tests, including protocol fixtures recorded from the real CLI (`Fixtures/*.jsonl`) and tests against a fake CLI subprocess.
6. **Licensing**: Python is MIT and can be ported; TypeScript is proprietary (© Anthropic), so the .NET package only follows the protocol messages it sends and ports none of its code.

## Feature matrix

Legend: ✅ Supported, ⭐ Extra, 🟡 Partial, ❌ No, — N/A (⭐ marks what goes beyond the official SDKs)

A ❌ for a feature that is only a CLI flag (such as `--add-dir` or `--fallback-model`) means .NET has no dedicated option for it; the flag can still be passed through `ExtraArguments`.

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
| **Resume a session** | ✅ `resume`, `continue_conversation`, `session_id`, `resume_session_at`, `resume_drops_turn` | ✅ `resume`, `continue`, `sessionId`, `resumeSessionAt`, `resumeDropsTurn` | ⭐ `ResumeSessionId`, `ContinueConversation`, `SessionId`, `ResumeSessionAt`, `ResumeDropsTurn`; conflicting combinations are rejected before connecting |
| **Fork a session** | ✅ `fork_session` option and function | ✅ `forkSession` | 🟡 `ForkSession` option; no forking function yet |
| **Skip saving the session** | ❌ | ✅ `persistSession: false` | ✅ `PersistSession = false` |
| **Send prompts verbatim** | ✅ `verbatim_prompts` | ✅ `verbatimPrompts` | ✅ `VerbatimPrompts` |
| **List / read history** | ✅ `list_sessions`, `get_session_info`, `get_session_messages`, `list_subagents` | ✅ `listSessions`, `getSessionMessages`, and more | ❌ planned |
| **Rename / tag / delete** | ✅ `rename_session`, `tag_session`, `delete_session` | ✅ `renameSession`, `tagSession`, `deleteSession` | ❌ planned |
| **Session store / transcript mirror** | ✅ `session_store`, `InMemorySessionStore` | ✅ `sessionStore` | ❌ not planned |
| **Interrupt** | ✅ `interrupt()` | ✅ `interrupt()` | ✅ `InterruptAsync`; pending permission prompts get their token cancelled |
| **Partial message streaming** | ✅ `include_partial_messages` | ✅ `includePartialMessages` | ⭐ `IncludePartialMessages`, plus `StreamEvent.TextDelta` for the text |
| **Turn / budget limits** | ✅ `max_turns`, `max_budget_usd`, `task_budget` | ✅ `maxTurns`, `maxBudgetUsd`, `taskBudget` | ✅ `MaxTurns`, `MaxBudgetUsd` (a `decimal` amount), `TaskBudget` |
| **Structured output** | ✅ `output_format` | ✅ `outputFormat` | ✅ `OutputSchema` (a `JsonObject`); the result is in `ResultMessage.StructuredOutput` |
| **File checkpoints / rewind** | ✅ `enable_file_checkpointing`, `rewind_files` | ✅ `enableFileCheckpointing`, `rewindFiles` (with `dryRun`) | ⭐ `EnableFileCheckpointing`, `RewindFilesAsync` returning a `RewindFilesResult` and accepting `dryRun` (Python has neither) |

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
| **Allowed / disallowed tools** | ✅ `tools`, `allowed_tools`, `disallowed_tools` | ✅ `tools`, `allowedTools`, `disallowedTools`, `toolAliases`, `toolConfig` | ✅ `Tools`, `AllowedTools`, `DisallowedTools`, `ToolAliases`, `AskUserQuestionPreviewFormat` (the only entry in TS's `toolConfig` today) |
| **Permission modes** | ✅ `permission_mode`, `set_permission_mode()` | ✅ `permissionMode`, `setPermissionMode()`, `allowDangerouslySkipPermissions`, `planModeInstructions` | ⭐ `PermissionMode` (a wrapped string, so modes the CLI adds later still work), `SetPermissionModeAsync` returning the mode in effect, `AllowDangerouslySkipPermissions`, `PlanModeInstructions` |
| **Permission prompt tool** | ✅ `permission_prompt_tool_name` | ✅ `permissionPromptToolName`, `permissionPrompts` | ✅ `PermissionPromptToolName`, `DisablePermissionPrompts`; rejected together with `CanUseTool` |
| **Hook callbacks** | ✅ `hooks` (`HookMatcher`, more than twenty events; `on_failure` decides whether a failed callback blocks the action) | ✅ `hooks` (matchers take `onFailure`) | ❌ planned |
| **In-SDK MCP servers** | ✅ `create_sdk_mcp_server`, `@tool` | ✅ `createSdkMcpServer`, `tool` | ❌ planned; for now, host your own MCP server (for example an HTTP endpoint in ASP.NET Core) and connect it through `McpServers` |
| **External MCP servers** | ✅ `mcp_servers`, `strict_mcp_config` | ✅ `mcpServers`, `setMcpServers()` | ✅ `McpServers` (stdio, SSE, HTTP, including TS-only `timeout`, `alwaysLoad`, and tool permission policies), `StrictMcpConfig`, `SetMcpServersAsync` |
| **MCP status / reconnect / toggle** | ✅ `get_mcp_status`, `reconnect_mcp_server`, `toggle_mcp_server` | ✅ `mcpServerStatus()`, `reconnectMcpServer()`, `toggleMcpServer()`, `readMcpResource()`, `setMcpPermissionModeOverride()` | ✅ `GetMcpStatusAsync` (typed `McpServerStatus`), `ReconnectMcpServerAsync`, `ToggleMcpServerAsync`, `ReadMcpResourceAsync`, `SetMcpPermissionModeOverrideAsync` |
| **Subagent definitions** | ✅ `agents` (`AgentDefinition`), `forward_subagent_text` | ✅ `agents`, `agent`, `forwardSubagentText`, `agentProgressSummaries` | ✅ `Agents` (`AgentDefinition`, including TS-only fields), `Agent`, `ForwardSubagentText`, `AgentProgressSummaries` |
| **Background tasks** | ✅ `stop_task()`, `Task*Message` | ✅ `stopTask()`, `backgroundTasks()`, `perTaskStopAffordance` | ✅ `StopTaskAsync`, `BackgroundTasksAsync`, `PerTaskStopAffordance`; `TaskStartedMessage` and the other three task messages plus TS-only `BackgroundTasksChangedMessage`, all subtypes of `SystemMessage` |
| **Sandbox settings** | ✅ `sandbox` | ✅ `sandbox` | ✅ `Sandbox` (a `JsonObject` merged into `--settings`; rejected when `Settings` also has a `sandbox`, rather than merged key by key as TS does) |
| **Setting sources / plugins / skills** | ✅ `setting_sources`, `settings`, `plugins`, `skills` | ✅ `settingSources`, `settings`, `managedSettings`, `plugins`, `pluginDelivery`, `skills`, `reloadPlugins()`, `reloadSkills()` | ✅ `SettingSources`, `Settings` / `SettingsFile`, `ManagedSettings`, `Plugins` (with `SkipMcpDiscovery`), `PluginDelivery`, `Skills`, `ReloadPluginsAsync`, `ReloadSkillsAsync` |
| **Context usage** | ✅ `get_context_usage()` | ✅ `getContextUsage()`, with `detail: 'summary' \| 'full'` | ⭐ `GetContextUsageAsync` returning a typed `ContextUsage` (including the category `id` and `kind` that Python's types lack); `detail` follows TS |

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
| Code | about 13,500 lines of Python | shipped as a bundle; the `sdk.d.ts` types are about 10,500 lines | about 4,960 hand-written lines of C# (82 files) |
| Public client methods | 15 on `ClaudeSDKClient` | 31 on `Query` | 20 async methods on `ClaudeAgentClient` |
| Options | 49 | 69 | 50 |
| Tests | pytest | not published | 325 (xUnit), including fixtures recorded from the real CLI and a fake CLI subprocess |
| License | MIT | proprietary (© Anthropic) | MIT, with the Python SDK's notice kept in NOTICE |
| AOT | — | — | `IsAotCompatible` |

## Suggested next steps

- **Hook callbacks**: the CLI sends `hook_callback`, which can share the dispatch, cancellation, and error replies `CanUseTool` uses. With permission modes in place, hooks are the only way to see every tool call (`CanUseTool` is called only when the CLI would otherwise ask).
- **Session functions**: read-only ones first (listing, single-session info, reading transcripts and subagent transcripts), then the ones that write (renaming, tagging, deleting, forking). They read and write Claude Code's jsonl files directly; Python spends about 2,500 lines on them.
- **In-SDK MCP servers**: the CLI sends `mcp_message` (JSON-RPC), so the SDK has to act as an MCP server; under AOT, tool schemas cannot be generated by reflection.
- Options still missing in other areas: `AddDirectories`, `FallbackModel`, `Betas`, thinking settings, and TypeScript-only ones such as `projectConfigRoot` and `promptSuggestions`.
