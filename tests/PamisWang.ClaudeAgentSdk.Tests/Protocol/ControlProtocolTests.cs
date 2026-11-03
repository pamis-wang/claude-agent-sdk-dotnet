using System.Text.Json;
using System.Text.Json.Nodes;
using PamisWang.ClaudeAgentSdk.Mcp;
using PamisWang.ClaudeAgentSdk.Permissions;
using PamisWang.ClaudeAgentSdk.Protocol;

namespace PamisWang.ClaudeAgentSdk.Tests.Protocol;

/// <summary>樣本裡沒有的情況：錯誤回應、逾時、讀取失敗、不支援的請求、回呼出錯與關閉，以及換模型、套用設定送出的格式。</summary>
public sealed class ControlProtocolTests : IAsyncLifetime, IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly ScriptedTransport _transport = new();
    private ControlProtocol? _protocol;

    public Task InitializeAsync() => Task.CompletedTask;

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    public async ValueTask DisposeAsync()
    {
        if (_protocol is not null)
        {
            await _protocol.DisposeAsync();
        }

        await _transport.DisposeAsync();
    }

    [Fact]
    public async Task InitializeAsync_SendsInitializeAndReturnsResponse()
    {
        var protocol = Start();

        var initializing = protocol.InitializeAsync(Timeout);
        var request = await _transport.NextWriteAsync();
        var requestId = request.GetProperty("request_id").GetString()!;
        _transport.Push("""{"type":"control_response","response":{"subtype":"success","request_id":"ID","response":{"commands":[]}}}"""
            .Replace("ID", requestId, StringComparison.Ordinal));
        var response = await initializing;

        Assert.Matches("^req_1_[0-9a-f]{8}$", requestId);
        Assert.Equal(
            $$$"""{"type":"control_request","request_id":"{{{requestId}}}","request":{"subtype":"initialize","hooks":null}}""",
            request.GetRawText());
        Assert.Equal(JsonValueKind.Array, response.GetProperty("commands").ValueKind);
    }

    [Fact]
    public async Task SendControlRequestAsync_RequestIdsCountUp()
    {
        var protocol = Start();

        _ = protocol.InterruptAsync();
        _ = protocol.InterruptAsync();
        var ids = new[] { await _transport.NextWriteAsync(), await _transport.NextWriteAsync() }
            .Select(message => message.GetProperty("request_id").GetString()!.Split('_')[1])
            .Order();

        Assert.Equal(["1", "2"], ids);
    }

    [Fact]
    public async Task SendControlRequestAsync_ErrorResponse_ThrowsWithCliMessage()
    {
        var protocol = Start();

        var interrupting = protocol.InterruptAsync();
        var requestId = (await _transport.NextWriteAsync()).GetProperty("request_id").GetString();
        _transport.Push($$$"""{"type":"control_response","response":{"subtype":"error","request_id":"{{{requestId}}}","error":"沒有在跑的回合"}}""");

        var error = await Assert.ThrowsAsync<ClaudeAgentException>(() => interrupting);
        Assert.Equal("沒有在跑的回合", error.Message);
    }

    [Fact]
    public async Task SendControlRequestAsync_NoResponse_TimesOut()
    {
        var protocol = Start();

        var error = await Assert.ThrowsAsync<TimeoutException>(
            () => protocol.SendControlRequestAsync("interrupt", null, TimeSpan.FromMilliseconds(100)));
        Assert.Contains("interrupt", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendControlRequestAsync_OutputEndsWhileWaiting_FailsWithoutWaitingForTimeout()
    {
        var protocol = Start();

        var initializing = protocol.InitializeAsync(TimeSpan.FromMinutes(5));
        await _transport.NextWriteAsync();
        _transport.End();

        await Assert.ThrowsAsync<CliConnectionException>(() => initializing.WaitAsync(Timeout));
    }

    [Fact]
    public async Task SendControlRequestAsync_BeforeStart_Throws()
    {
        _protocol = new ControlProtocol(_transport, canUseTool: null);

        await Assert.ThrowsAsync<CliConnectionException>(() => _protocol.InterruptAsync());
    }

    [Theory]
    [InlineData("sonnet", """{"subtype":"set_model","model":"sonnet"}""")]
    [InlineData(null, """{"subtype":"set_model","model":null}""")]
    public async Task SetModelAsync_SendsModelOrNullForDefault(string? model, string expectedRequest)
    {
        var protocol = Start();

        var setting = protocol.SetModelAsync(model);
        var request = await _transport.NextWriteAsync();
        RespondSuccess(request);
        await setting.WaitAsync(Timeout);

        Assert.Equal(expectedRequest, request.GetProperty("request").GetRawText());
    }

    [Theory]
    [InlineData(null, """{"subtype":"get_context_usage"}""")]
    [InlineData(ContextUsageDetail.Full, """{"subtype":"get_context_usage","detail":"full"}""")]
    [InlineData(ContextUsageDetail.Summary, """{"subtype":"get_context_usage","detail":"summary"}""")]
    public async Task GetContextUsageAsync_SendsDetailOnlyWhenGiven(ContextUsageDetail? detail, string expectedRequest)
    {
        var protocol = Start();

        var querying = protocol.GetContextUsageAsync(detail);
        var request = await _transport.NextWriteAsync();
        RespondSuccess(request);
        await querying.WaitAsync(Timeout);

        Assert.Equal(expectedRequest, request.GetProperty("request").GetRawText());
    }

    [Fact]
    public async Task GetContextUsageAsync_UndefinedDetail_ThrowsWithoutSending()
    {
        var protocol = Start();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => protocol.GetContextUsageAsync((ContextUsageDetail)42));
        Assert.True(_transport.HasNoPendingWrites);
    }

    [Fact]
    public async Task ApplyFlagSettingsAsync_SendsSettingsKeepingNullsThatClearKeys()
    {
        var protocol = Start();

        var applying = protocol.ApplyFlagSettingsAsync(new JsonObject { ["effortLevel"] = "max", ["model"] = null });
        var request = await _transport.NextWriteAsync();
        RespondSuccess(request);
        await applying.WaitAsync(Timeout);

        Assert.Equal(
            """{"subtype":"apply_flag_settings","settings":{"effortLevel":"max","model":null}}""",
            request.GetProperty("request").GetRawText());
    }

    [Fact]
    public async Task ReceiveMessagesAsync_TransportFails_RethrowsOriginalExceptionToReaderAndPendingRequest()
    {
        var protocol = Start();
        var initializing = protocol.InitializeAsync(Timeout);
        await _transport.NextWriteAsync();

        _transport.Fail(new CliProcessException("Claude Code 異常結束", 1));

        var readError = await Assert.ThrowsAsync<CliProcessException>(() => ReadAllAsync(protocol));
        var requestError = await Assert.ThrowsAsync<CliProcessException>(() => initializing);
        Assert.Equal(1, readError.ExitCode);
        Assert.Same(readError, requestError);
    }

    [Fact]
    public async Task ReceiveMessagesAsync_PassesOnlyNonControlMessages()
    {
        var protocol = Start();

        _transport.Push("""{"type":"system","subtype":"session_state_changed","state":"running","sdk_host_only":true}""");
        _transport.Push("""{"type":"system","subtype":"session_state_changed","state":"idle"}""");
        _transport.Push("""{"type":"transcript_mirror","filePath":"x","entries":[]}""");
        _transport.Push("""{"type":"control_response","response":{"subtype":"success","request_id":"req_9_unknown"}}""");
        _transport.Push("""{"type":"control_cancel_request","request_id":"unknown"}""");
        _transport.Push("""{"type":"assistant","message":{"content":[]}}""");
        _transport.Push("""{"type":"rate_limit_event"}""");
        _transport.End();
        var messages = await ReadAllAsync(protocol);

        Assert.Equal(
            ["system", "assistant", "rate_limit_event"],
            messages.Select(message => message.GetProperty("type").GetString()));
        Assert.Equal("idle", messages[0].GetProperty("state").GetString());
    }

    [Fact]
    public async Task ControlRequest_UnsupportedSubtype_RespondsWithError()
    {
        Start();

        _transport.Push("""{"type":"control_request","request_id":"cli_1","request":{"subtype":"hook_callback","callback_id":"hook_0"}}""");
        var response = (await _transport.NextWriteAsync()).GetProperty("response");

        Assert.Equal("error", response.GetProperty("subtype").GetString());
        Assert.Equal("cli_1", response.GetProperty("request_id").GetString());
        Assert.Contains("hook_callback", response.GetProperty("error").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CanUseTool_NotConfigured_RespondsWithError()
    {
        Start();

        _transport.Push(PermissionRequest("cli_1"));
        var response = (await _transport.NextWriteAsync()).GetProperty("response");

        Assert.Equal("error", response.GetProperty("subtype").GetString());
        Assert.Contains("CanUseTool", response.GetProperty("error").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CanUseTool_CallbackThrows_RespondsWithItsMessage()
    {
        Start((_, _) => throw new InvalidOperationException("審核服務掛了"));

        _transport.Push(PermissionRequest("cli_1"));
        var response = (await _transport.NextWriteAsync()).GetProperty("response");

        Assert.Equal("error", response.GetProperty("subtype").GetString());
        Assert.Equal("審核服務掛了", response.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CanUseTool_AllowWithUpdatedInput_SendsUpdatedInput()
    {
        var updated = JsonSerializer.Deserialize<JsonElement>("""{"command":"npm test -- --ci"}""");
        Start((_, _) => Task.FromResult<PermissionResult>(new PermissionResultAllow(UpdatedInput: updated)));

        _transport.Push(PermissionRequest("cli_1"));
        var response = (await _transport.NextWriteAsync()).GetProperty("response").GetProperty("response");

        Assert.Equal("""{"behavior":"allow","updatedInput":{"command":"npm test -- --ci"}}""", response.GetRawText());
    }

    [Fact]
    public async Task CanUseTool_DenyWithInterrupt_SendsInterruptFlag()
    {
        Start((_, _) => Task.FromResult<PermissionResult>(new PermissionResultDeny("不行", Interrupt: true)));

        _transport.Push(PermissionRequest("cli_1"));
        var response = (await _transport.NextWriteAsync()).GetProperty("response").GetProperty("response");

        Assert.Equal("deny", response.GetProperty("behavior").GetString());
        Assert.Equal("不行", response.GetProperty("message").GetString());
        Assert.True(response.GetProperty("interrupt").GetBoolean());
    }

    [Fact]
    public async Task CanUseTool_WaitingForUser_DoesNotBlockOtherMessages()
    {
        var answer = new TaskCompletionSource<PermissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var protocol = Start((_, _) => answer.Task);
        await using var messages = protocol.ReceiveMessagesAsync().GetAsyncEnumerator();

        _transport.Push(PermissionRequest("cli_1"));
        _transport.Push("""{"type":"assistant","message":{"content":[]}}""");

        Assert.True(await messages.MoveNextAsync().AsTask().WaitAsync(Timeout));
        Assert.Equal("assistant", messages.Current.GetProperty("type").GetString());
        Assert.True(_transport.HasNoPendingWrites);

        answer.SetResult(new PermissionResultAllow());
        var response = (await _transport.NextWriteAsync()).GetProperty("response");
        Assert.Equal("cli_1", response.GetProperty("request_id").GetString());
    }

    [Fact]
    public async Task DisposeAsync_CancelsWaitingCallbackAndClosesTransport()
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var asked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var protocol = Start(async (_, cancellationToken) =>
        {
            asked.SetResult();
            using var registration = cancellationToken.Register(cancelled.SetResult);
            await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
            return new PermissionResultAllow();
        });
        _transport.Push(PermissionRequest("cli_1"));
        await asked.Task.WaitAsync(Timeout);

        await protocol.DisposeAsync();

        await cancelled.Task.WaitAsync(Timeout);
        Assert.True(_transport.Disposed);
        Assert.True(_transport.HasNoPendingWrites);
    }

    [Theory]
    [InlineData("set_permission_mode", """{"subtype":"set_permission_mode","mode":"auto"}""")]
    [InlineData("rewind_files", """{"subtype":"rewind_files","user_message_id":"u1"}""")]
    [InlineData("rewind_files_dry_run", """{"subtype":"rewind_files","user_message_id":"u1","dry_run":true}""")]
    [InlineData("stop_task", """{"subtype":"stop_task","task_id":"t1"}""")]
    [InlineData("background_tasks", """{"subtype":"background_tasks"}""")]
    [InlineData("background_tasks_one", """{"subtype":"background_tasks","tool_use_id":"toolu_1"}""")]
    [InlineData("mcp_status", """{"subtype":"mcp_status"}""")]
    [InlineData("mcp_reconnect", """{"subtype":"mcp_reconnect","serverName":"docs"}""")]
    [InlineData("mcp_toggle", """{"subtype":"mcp_toggle","serverName":"docs","enabled":false}""")]
    [InlineData(
        "mcp_set_servers",
        """{"subtype":"mcp_set_servers","servers":{"docs":{"type":"sse","url":"https://example.com/sse","headers":{"Authorization":"Bearer t"},"tools":[{"name":"search","permission_policy":"always_ask"}],"timeout":30000,"alwaysLoad":true}}}""")]
    [InlineData("mcp_read_resource", """{"subtype":"mcp_read_resource","serverName":"docs","uri":"ui://docs/view"}""")]
    [InlineData("set_mcp_permission_mode_override", """{"subtype":"set_mcp_permission_mode_override","serverName":"docs","mode":"default"}""")]
    [InlineData("set_mcp_permission_mode_override_clear", """{"subtype":"set_mcp_permission_mode_override","serverName":"docs","mode":null}""")]
    [InlineData("reload_plugins", """{"subtype":"reload_plugins"}""")]
    [InlineData(
        "reload_plugins_hold_and_add",
        """{"subtype":"reload_plugins","hold_on_cache_impact":true,"add_plugins":[{"type":"local","path":"/plugins/a","skipMcpDiscovery":true}]}""")]
    [InlineData("reload_skills", """{"subtype":"reload_skills"}""")]
    public async Task ControlRequests_SendTheSameFieldsAsTheOfficialSdks(string request, string expectedRequest)
    {
        var protocol = Start();

        Task sending = request switch
        {
            "set_permission_mode" => protocol.SetPermissionModeAsync("auto"),
            "rewind_files" => protocol.RewindFilesAsync("u1", dryRun: false),
            "rewind_files_dry_run" => protocol.RewindFilesAsync("u1", dryRun: true),
            "stop_task" => protocol.StopTaskAsync("t1"),
            "background_tasks" => protocol.BackgroundTasksAsync(toolUseId: null),
            "background_tasks_one" => protocol.BackgroundTasksAsync("toolu_1"),
            "mcp_status" => protocol.GetMcpStatusAsync(),
            "mcp_reconnect" => protocol.ReconnectMcpServerAsync("docs"),
            "mcp_toggle" => protocol.ToggleMcpServerAsync("docs", enabled: false),
            "mcp_set_servers" => protocol.SetMcpServersAsync(new Dictionary<string, McpServerConfig>
            {
                ["docs"] = new McpSseServerConfig("https://example.com/sse")
                {
                    Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer t" },
                    Tools = [new McpToolPolicy("search") { PermissionPolicy = "always_ask" }],
                    Timeout = 30000,
                    AlwaysLoad = true,
                },
            }),
            "mcp_read_resource" => protocol.ReadMcpResourceAsync("docs", "ui://docs/view"),
            "set_mcp_permission_mode_override" => protocol.SetMcpPermissionModeOverrideAsync("docs", "default"),
            "set_mcp_permission_mode_override_clear" => protocol.SetMcpPermissionModeOverrideAsync("docs", mode: null),
            "reload_plugins" => protocol.ReloadPluginsAsync(holdOnCacheImpact: false, addPlugins: []),
            "reload_plugins_hold_and_add" => protocol.ReloadPluginsAsync(
                holdOnCacheImpact: true,
                addPlugins: [new LocalPlugin("/plugins/a") { SkipMcpDiscovery = true }]),
            "reload_skills" => protocol.ReloadSkillsAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request, null),
        };
        var written = await _transport.NextWriteAsync();
        RespondSuccess(written);
        await sending.WaitAsync(Timeout);

        Assert.Equal(expectedRequest, written.GetProperty("request").GetRawText());
    }

    [Fact]
    public async Task InitializeAsync_WithOptions_SendsAgentsSkillsAndTypeScriptOnlyFields()
    {
        _protocol = new ControlProtocol(_transport, canUseTool: null)
        {
            InitializeOptions = new ClaudeAgentOptions
            {
                Agents = new Dictionary<string, AgentDefinition>
                {
                    ["reviewer"] = new("Reviews code.", "You review code.")
                    {
                        Tools = ["Read", "Grep"],
                        Model = "haiku",
                        Memory = "project",
                        McpServers = ["docs"],
                        InlineMcpServers = new Dictionary<string, McpServerConfig> { ["lint"] = new McpHttpServerConfig("http://127.0.0.1:5000/mcp") },
                        MaxTurns = 5,
                        Background = true,
                        Effort = "low",
                        PermissionMode = PermissionMode.Plan,
                        OmitClaudeMd = true,
                    },
                },
                Skills = SkillSelection.Only("pdf", "docx"),
                ForwardSubagentText = true,
                PlanModeInstructions = "List the steps only.",
                ToolAliases = new Dictionary<string, string> { ["Bash"] = "mcp__workspace__bash" },
                AgentProgressSummaries = true,
                PerTaskStopAffordance = true,
                Plugins = [new LocalPlugin("/plugins/a")],
                PluginDelivery = PluginDelivery.Initialize,
            },
        };
        _protocol.Start();

        var initializing = _protocol.InitializeAsync(Timeout);
        var request = await _transport.NextWriteAsync();
        RespondSuccess(request);
        await initializing;

        Assert.Equal(
            """
            {"subtype":"initialize","hooks":null,
            "agents":{"reviewer":{"description":"Reviews code.","prompt":"You review code.","tools":["Read","Grep"],"model":"haiku","memory":"project",
            "mcpServers":["docs",{"lint":{"type":"http","url":"http://127.0.0.1:5000/mcp"}}],"maxTurns":5,"background":true,"effort":"low","permissionMode":"plan","omitClaudeMd":true}},
            "skills":["pdf","docx"],"forwardSubagentText":true,"planModeInstructions":"List the steps only.",
            "toolAliases":{"Bash":"mcp__workspace__bash"},"agentProgressSummaries":true,"perTaskStopAffordance":true,
            "plugins":[{"type":"local","path":"/plugins/a"}]}
            """.ReplaceLineEndings(""),
            request.GetProperty("request").GetRawText());
    }

    [Fact]
    public async Task InitializeAsync_DefaultOptions_SendsOnlyHooksLikePythonSdk()
    {
        _protocol = new ControlProtocol(_transport, canUseTool: null) { InitializeOptions = new ClaudeAgentOptions { Skills = SkillSelection.All } };
        _protocol.Start();

        var initializing = _protocol.InitializeAsync(Timeout);
        var request = await _transport.NextWriteAsync();
        RespondSuccess(request);
        await initializing;

        // 全部的 skill 只影響 --allowedTools，不送清單
        Assert.Equal("""{"subtype":"initialize","hooks":null}""", request.GetProperty("request").GetRawText());
    }

    [Fact]
    public async Task SendUserMessageAsync_VerbatimPrompts_MarksMessageClientComposed()
    {
        _protocol = new ControlProtocol(_transport, canUseTool: null) { VerbatimPrompts = true };
        _protocol.Start();

        await _protocol.SendUserMessageAsync("@notes.md 照原文");
        var message = await _transport.NextWriteAsync();

        Assert.True(message.GetProperty("client_composed").GetBoolean());
        Assert.Equal("@notes.md 照原文", message.GetProperty("message").GetProperty("content").GetString());
    }

    [Fact]
    public async Task SetPermissionModeAsync_ReturnsModeCliEchoes()
    {
        var protocol = Start();

        var setting = protocol.SetPermissionModeAsync("acceptEdits");
        var request = await _transport.NextWriteAsync();
        _transport.Push("""{"type":"control_response","response":{"subtype":"success","request_id":"ID","response":{"mode":"acceptEdits"}}}"""
            .Replace("ID", request.GetProperty("request_id").GetString(), StringComparison.Ordinal));

        Assert.Equal("acceptEdits", await setting.WaitAsync(Timeout));
    }

    private ControlProtocol Start(Func<ToolPermissionRequest, CancellationToken, Task<PermissionResult>>? canUseTool = null)
    {
        _protocol = new ControlProtocol(_transport, canUseTool) { HandlerShutdownTimeout = TimeSpan.FromSeconds(1) };
        _protocol.Start();
        return _protocol;
    }

    private void RespondSuccess(JsonElement request) =>
        _transport.Push($$$"""{"type":"control_response","response":{"subtype":"success","request_id":"{{{request.GetProperty("request_id").GetString()}}}"}}""");

    private static string PermissionRequest(string requestId) =>
        $$$"""{"type":"control_request","request_id":"{{{requestId}}}","request":{"subtype":"can_use_tool","tool_name":"Bash","input":{"command":"npm test"},"permission_suggestions":[],"tool_use_id":"toolu_1"}}""";

    private static async Task<List<JsonElement>> ReadAllAsync(ControlProtocol protocol)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        List<JsonElement> messages = [];
        await foreach (var message in protocol.ReceiveMessagesAsync(timeout.Token))
        {
            messages.Add(message);
        }

        return messages;
    }
}
