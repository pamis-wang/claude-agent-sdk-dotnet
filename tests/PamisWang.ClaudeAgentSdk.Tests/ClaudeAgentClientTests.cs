using System.Text.Json;
using System.Text.Json.Nodes;
using PamisWang.ClaudeAgentSdk.Mcp;
using PamisWang.ClaudeAgentSdk.Messages;
using PamisWang.ClaudeAgentSdk.Permissions;
using PamisWang.ClaudeAgentSdk.Tests.Protocol;

namespace PamisWang.ClaudeAgentSdk.Tests;

public sealed class ClaudeAgentClientTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly TempDirectory _cwd = new();

    public void Dispose() => _cwd.Dispose();

    [Fact]
    public async Task ReceiveResponseAsync_RecordedTurn_YieldsTypedMessagesEndingWithResult()
    {
        var fixture = ProtocolFixture.Load("basic");
        await using var client = new ClaudeAgentClient(new ClaudeAgentOptions(), new ReplayTransport(fixture));

        await client.ConnectAsync();
        await client.SendAsync(fixture.UserPrompts[0]);
        var messages = await CollectAsync(client.ReceiveResponseAsync());

        Assert.True(client.IsConnected);
        Assert.Equal(fixture.Meta.GetProperty("cli_version").GetString(), client.ServerInfo?.GetProperty("claude_code_version").GetString());
        Assert.Equal("init", Assert.IsType<SystemMessage>(messages[0]).Subtype);
        Assert.Contains(messages, message => message is AssistantMessage);
        Assert.IsType<ResultMessage>(messages[^1]);
    }

    [Fact]
    public async Task ReceiveResponseAsync_TwoTurns_EachEndsAtItsResult()
    {
        var fixture = ProtocolFixture.Load("multi_turn");
        await using var client = new ClaudeAgentClient(new ClaudeAgentOptions(), new ReplayTransport(fixture));
        await client.ConnectAsync();

        await client.SendAsync(fixture.UserPrompts[0]);
        var first = (await CollectAsync(client.ReceiveResponseAsync())).OfType<ResultMessage>().Single();
        await client.SendAsync(fixture.UserPrompts[1]);
        var second = (await CollectAsync(client.ReceiveResponseAsync())).OfType<ResultMessage>().Single();

        Assert.Equal("好", first.Result);
        Assert.Equal("鳳梨酥", second.Result);
        Assert.Equal(first.SessionId, second.SessionId);
    }

    [Fact]
    public async Task CanUseTool_RecordedPermission_IsAnsweredThroughOptions()
    {
        var fixture = ProtocolFixture.Load("permission_allow");
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (_, _) => Task.FromResult<PermissionResult>(new PermissionResultAllow()),
        };
        await using var client = new ClaudeAgentClient(options, new ReplayTransport(fixture));
        await client.ConnectAsync();

        await client.SendAsync(fixture.UserPrompts[0]);
        var messages = await CollectAsync(client.ReceiveResponseAsync());

        var toolResult = messages.OfType<UserMessage>().SelectMany(m => m.Content).OfType<ToolResultBlock>().Single();
        Assert.NotEqual(true, toolResult.IsError);
        Assert.Equal("success", messages.OfType<ResultMessage>().Single().Subtype);
    }

    [Fact]
    public async Task InterruptAsync_RecordedInterrupt_EndsTurnWithErrorResult()
    {
        var fixture = ProtocolFixture.Load("interrupt");
        await using var client = new ClaudeAgentClient(new ClaudeAgentOptions(), new ReplayTransport(fixture));
        await client.ConnectAsync();
        await client.SendAsync(fixture.UserPrompts[0]);
        ResultMessage? result = null;

        var interrupted = false;
        await foreach (var message in client.ReceiveResponseAsync())
        {
            if (!interrupted && message is StreamEvent { EventType: "content_block_delta" })
            {
                interrupted = true;
                await client.InterruptAsync();
            }

            result = message as ResultMessage;
        }

        Assert.True(result?.IsError);
        Assert.Equal("aborted_streaming", result?.TerminalReason);
    }

    [Fact]
    public async Task EndToEnd_FakeCliProcess_ChatsAndAnswersPermission()
    {
        ToolPermissionRequest? asked = null;
        var options = new ClaudeAgentOptions
        {
            CliPath = FakeClaude.Path,
            WorkingDirectory = _cwd.Path,
            EnvironmentVariables = new Dictionary<string, string> { ["FAKE_CLAUDE_MODE"] = "chat" },
            CanUseTool = (request, _) =>
            {
                asked = request;
                return Task.FromResult<PermissionResult>(new PermissionResultDeny("不行"));
            },
        };
        await using var client = new ClaudeAgentClient(options);
        await client.ConnectAsync().WaitAsync(Timeout);

        await client.SendAsync("你好 🐱");
        var reply = await CollectAsync(client.ReceiveResponseAsync());
        await client.SendAsync("請求權限");
        var permission = await CollectAsync(client.ReceiveResponseAsync());
        await client.InterruptAsync().WaitAsync(Timeout);

        Assert.Equal("fake", client.ServerInfo?.GetProperty("claude_code_version").GetString());
        Assert.Equal("收到：你好 🐱", reply.OfType<AssistantMessage>().Single().Content.OfType<TextBlock>().Single().Text);
        Assert.Equal("Write", asked?.ToolName);
        Assert.Equal("deny", permission.OfType<ResultMessage>().Single().Result);
    }

    [Fact]
    public async Task SetModelAsync_FakeCliProcess_LaterRepliesUseNewModel()
    {
        var options = new ClaudeAgentOptions
        {
            CliPath = FakeClaude.Path,
            WorkingDirectory = _cwd.Path,
            EnvironmentVariables = new Dictionary<string, string> { ["FAKE_CLAUDE_MODE"] = "chat" },
        };
        await using var client = new ClaudeAgentClient(options);
        await client.ConnectAsync().WaitAsync(Timeout);

        var switchedTo = await client.SetModelAsync("sonnet").WaitAsync(Timeout);
        await client.SendAsync("你好");
        var switched = await CollectAsync(client.ReceiveResponseAsync());
        var resetTo = await client.SetModelAsync().WaitAsync(Timeout);
        await client.SendAsync("你好");
        var reset = await CollectAsync(client.ReceiveResponseAsync());

        Assert.Equal("sonnet", switchedTo);
        Assert.Equal("sonnet", switched.OfType<AssistantMessage>().Single().Model);
        Assert.Equal("fake", resetTo);
        Assert.Equal("fake", reset.OfType<AssistantMessage>().Single().Model);
    }

    [Fact]
    public async Task SetModelAsync_RecordedSwitch_ReturnsResolvedModelAndNextTurnUsesIt()
    {
        var fixture = ProtocolFixture.Load("set_model");
        await using var client = new ClaudeAgentClient(new ClaudeAgentOptions(), new ReplayTransport(fixture));
        await client.ConnectAsync();

        await client.SendAsync(fixture.UserPrompts[0]);
        await CollectAsync(client.ReceiveResponseAsync());
        var model = await client.SetModelAsync("sonnet");
        await client.SendAsync(fixture.UserPrompts[1]);
        var messages = await CollectAsync(client.ReceiveResponseAsync());

        Assert.Equal("claude-sonnet-5-5", model);
        // CLI 把換模型的結果當成一則重播的使用者訊息送來
        var notice = messages.OfType<UserMessage>().Single();
        Assert.True(notice.Raw.GetProperty("isReplay").GetBoolean());
        Assert.Contains("Set model to", notice.Content.OfType<TextBlock>().Single().Text, StringComparison.Ordinal);
        Assert.Equal("claude-sonnet-5-5", messages.OfType<AssistantMessage>().Single().Model);
    }

    [Fact]
    public async Task GetContextUsageAsync_RecordedBeforeAndAfterTurn_ParsesUsage()
    {
        var fixture = ProtocolFixture.Load("context_usage");
        await using var client = new ClaudeAgentClient(new ClaudeAgentOptions(), new ReplayTransport(fixture));
        await client.ConnectAsync();

        // 重播會比對送出的 request：不指定 detail 時不帶這個欄位，跟 Python SDK 一樣
        var before = await client.GetContextUsageAsync();
        await client.SendAsync(fixture.UserPrompts[0]);
        await CollectAsync(client.ReceiveResponseAsync());
        var after = await client.GetContextUsageAsync(ContextUsageDetail.Summary);

        Assert.Equal("claude-haiku-5-5", before.Model);
        Assert.Equal(16410, before.TotalTokens);
        Assert.Equal(1_000_000, before.MaxTokens);
        Assert.Equal(1_000_000, before.RawMaxTokens);
        Assert.Equal(2, before.Percentage);
        Assert.True(before.IsAutoCompactEnabled);
        Assert.Equal(967_000, before.AutoCompactThreshold);
        Assert.Equal(before.Raw.GetProperty("categories").GetArrayLength(), before.Categories.Count);
        var deferred = before.Categories.Single(category => category.Id == "system_tools_deferred");
        Assert.Equal("deferred", deferred.Kind);
        Assert.True(deferred.IsDeferred);
        Assert.Equal("free", before.Categories.Single(category => category.Id == "free_space").Kind);
        Assert.All(before.Categories.Where(category => category.Kind != "deferred"), category => Assert.False(category.IsDeferred));
        // 錄製時不載入使用者設定與 MCP，這些都是空的
        Assert.Empty(before.MemoryFiles);
        Assert.Empty(before.McpTools);
        Assert.Empty(before.Agents);
        // 沒有對應屬性的欄位從 Raw 讀；說過話之後才有 API 的用量
        Assert.Equal(JsonValueKind.Null, before.Raw.GetProperty("apiUsage").ValueKind);
        Assert.Equal(JsonValueKind.Object, after.Raw.GetProperty("apiUsage").ValueKind);
        Assert.True(after.TotalTokens > before.TotalTokens);
    }

    [Fact]
    public async Task SetPermissionModeAsync_RecordedSwitch_SkipsPermissionPromptAndReturnsMode()
    {
        var fixture = ProtocolFixture.Load("set_permission_mode");
        var asked = 0;
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (_, _) =>
            {
                Interlocked.Increment(ref asked);
                return Task.FromResult<PermissionResult>(new PermissionResultDeny("不該問"));
            },
        };
        await using var client = new ClaudeAgentClient(options, new ReplayTransport(fixture));
        await client.ConnectAsync();

        var acceptEdits = await client.SetPermissionModeAsync(PermissionMode.AcceptEdits);
        await client.SendAsync(fixture.UserPrompts[0]);
        var messages = await CollectAsync(client.ReceiveResponseAsync());
        var back = await client.SetPermissionModeAsync(PermissionMode.Default);

        Assert.Equal(PermissionMode.AcceptEdits, acceptEdits);
        Assert.Equal(PermissionMode.Default, back);
        // acceptEdits 下 Write 直接通過，沒有經過權限回呼
        Assert.Equal(0, asked);
        Assert.Contains(messages.OfType<AssistantMessage>().SelectMany(m => m.Content).OfType<ToolUseBlock>(), tool => tool.Name == "Write");
        Assert.False(messages.OfType<ResultMessage>().Single().IsError);
    }

    [Fact]
    public async Task Agents_RecordedSubagentRun_SendsDefinitionAndYieldsTaskMessages()
    {
        var fixture = ProtocolFixture.Load("subagent_task");
        // 重播會比對 initialize：定義要跟 Python SDK 送出的一樣
        var options = new ClaudeAgentOptions
        {
            Agents = new Dictionary<string, AgentDefinition>
            {
                ["echoer"] = new("Repeats the given word back. Use when asked to echo a word.", "You repeat the word you are given, nothing else.")
                {
                    Tools = [],
                    Model = "haiku",
                },
            },
        };
        await using var client = new ClaudeAgentClient(options, new ReplayTransport(fixture));
        await client.ConnectAsync();

        await client.SendAsync(fixture.UserPrompts[0]);
        var messages = await CollectAsync(client.ReceiveResponseAsync());

        var started = messages.OfType<TaskStartedMessage>().Single();
        Assert.Equal("echoer", started.SubagentType);
        Assert.True(messages.OfType<TaskUpdatedMessage>().Single(m => m.TaskId == started.TaskId).Patch.IsTerminal);
        Assert.Equal("completed", messages.OfType<TaskNotificationMessage>().Single().Status);
        Assert.Empty(messages.OfType<BackgroundTasksChangedMessage>().Last().Tasks);
    }

    [Fact]
    public async Task McpMethods_RecordedStatusToggleAndReconnect_ParseStatusAndSurfaceCliError()
    {
        var fixture = ProtocolFixture.Load("mcp_status");
        await using var client = new ClaudeAgentClient(new ClaudeAgentOptions(), new ReplayTransport(fixture));
        await client.ConnectAsync();

        var before = await client.GetMcpStatusAsync();
        await client.ToggleMcpServerAsync("ghost", enabled: false);
        var after = await client.GetMcpStatusAsync();
        var error = await Assert.ThrowsAsync<ClaudeAgentException>(() => client.ReconnectMcpServerAsync("ghost"));

        var ghost = before.Single();
        Assert.Equal("ghost", ghost.Name);
        Assert.Equal("pending", ghost.Status);
        Assert.Equal("nonexistent-mcp-server-for-fixtures", ghost.Config!.Value.GetProperty("command").GetString());
        Assert.Equal("disabled", after.Single().Status);
        Assert.Contains("disabled", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RewindFilesAsync_RecordedCheckpoint_PreviewsThenRestores()
    {
        var fixture = ProtocolFixture.Load("rewind_files");
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (_, _) => Task.FromResult<PermissionResult>(new PermissionResultAllow()),
        };
        await using var client = new ClaudeAgentClient(options, new ReplayTransport(fixture));
        await client.ConnectAsync();

        await client.SendAsync(fixture.UserPrompts[0]);
        var messages = await CollectAsync(client.ReceiveResponseAsync());
        // replay-user-messages 送回來的第一則使用者訊息就是剛才說的那句，帶著 uuid
        var userMessageId = messages.OfType<UserMessage>().First().Uuid!;
        var preview = await client.RewindFilesAsync(userMessageId, dryRun: true);
        var restored = await client.RewindFilesAsync(userMessageId);

        Assert.True(preview.CanRewind);
        Assert.EndsWith("hello.txt", preview.FilesChanged.Single(), StringComparison.Ordinal);
        Assert.Equal(0, preview.Insertions);
        Assert.Equal(1, preview.Deletions);
        Assert.Null(preview.SkippedLinks);
        Assert.True(restored.CanRewind);
        Assert.Equal(0, restored.SkippedLinks);
    }

    [Fact]
    public async Task TypeScriptOnlyRequests_Recorded_AreAcceptedByCli()
    {
        var fixture = ProtocolFixture.Load("ts_control_requests");
        await using var client = new ClaudeAgentClient(new ClaudeAgentOptions(), new ReplayTransport(fixture));
        await client.ConnectAsync();

        var set = await client.SetMcpServersAsync(new Dictionary<string, McpServerConfig> { ["ghost"] = new McpHttpServerConfig("http://127.0.0.1:9/mcp") });
        var pinned = await client.SetMcpPermissionModeOverrideAsync("ghost", PermissionMode.Default);
        var cleared = await client.SetMcpPermissionModeOverrideAsync("ghost", mode: null);
        var all = await client.BackgroundTasksAsync();
        var one = await client.BackgroundTasksAsync("toolu_nonexistent");
        await client.StopTaskAsync("nonexistent");

        Assert.Equal(["ghost"], set.Added);
        Assert.Empty(set.Removed);
        Assert.True(set.Errors.ContainsKey("ghost"));
        Assert.Null(pinned);
        Assert.Null(cleared);
        Assert.True(all);
        Assert.False(one);
    }

    [Fact]
    public async Task SendAsync_BeforeConnect_ThrowsCliConnectionException()
    {
        await using var client = new ClaudeAgentClient(new ClaudeAgentOptions(), new ScriptedTransport());

        await Assert.ThrowsAsync<CliConnectionException>(() => client.SendAsync("你好"));
        Assert.False(client.IsConnected);
        Assert.Null(client.ServerInfo);
    }

    [Fact]
    public async Task Methods_AfterDispose_ThrowObjectDisposed()
    {
        var client = new ClaudeAgentClient(new ClaudeAgentOptions(), new ReplayTransport(ProtocolFixture.Load("basic")));
        await client.ConnectAsync();

        await client.DisposeAsync();
        await client.DisposeAsync();

        Assert.False(client.IsConnected);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.SendAsync("你好"));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.InterruptAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.SetModelAsync("sonnet"));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.ApplyFlagSettingsAsync(new JsonObject { ["effortLevel"] = "low" }));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetContextUsageAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.ConnectAsync());
    }

    [Fact]
    public async Task ConnectAsync_Twice_Throws()
    {
        await using var client = new ClaudeAgentClient(new ClaudeAgentOptions(), new ReplayTransport(ProtocolFixture.Load("basic")));
        await client.ConnectAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ConnectAsync());
    }

    [Fact]
    public async Task ConnectAsync_CliMissing_ThrowsAndClientIsClosed()
    {
        var client = new ClaudeAgentClient(new ClaudeAgentOptions { CliPath = Path.Combine(_cwd.Path, "claude.exe"), WorkingDirectory = _cwd.Path });

        await Assert.ThrowsAsync<CliNotFoundException>(() => client.ConnectAsync());

        Assert.False(client.IsConnected);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.SendAsync("你好"));
        await client.DisposeAsync();
    }

    [Theory]
    [InlineData(null, 60)]
    [InlineData("", 60)]
    [InlineData("不是數字", 60)]
    [InlineData("1000", 60)]
    [InlineData("120000", 120)]
    public void InitializeTimeout_AtLeastSixtySeconds(string? environmentValue, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), ClaudeAgentClient.InitializeTimeout(environmentValue));
    }

    private static async Task<List<ClaudeMessage>> CollectAsync(IAsyncEnumerable<ClaudeMessage> messages)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        List<ClaudeMessage> collected = [];
        await foreach (var message in messages.WithCancellation(timeout.Token))
        {
            collected.Add(message);
        }

        return collected;
    }
}
