using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using PamisWang.ClaudeAgentSdk.Transport;

namespace PamisWang.ClaudeAgentSdk.Tests.Transport;

/// <summary>用假的 CLI（FakeClaude）跑真的子程序，驗證參數、編碼、stderr、結束代碼與收尾。</summary>
public sealed class SubprocessTransportTests : IDisposable
{
    private readonly TempDirectory _cwd = new();

    public void Dispose() => _cwd.Dispose();

    [Fact]
    public async Task ConnectAsync_StartsCliWithArgumentsWorkingDirectoryAndEnvironment()
    {
        var options = Options("echo", env: new() { ["FAKE_CLAUDE_EXTRA"] = "額外的值" });
        await using var transport = new SubprocessTransport(options);

        await transport.ConnectAsync();
        await transport.EndInputAsync();
        var report = Assert.Single(await ReadAllAsync(transport));

        // 包含 --system-prompt 後面那個空字串：空參數也要原樣送到
        Assert.Equal(
            CliArguments.Build(options, OperatingSystem.IsWindows()),
            report.GetProperty("argv").EnumerateArray().Select(arg => arg.GetString()));
        // macOS 的暫存資料夾經過符號連結（/var → /private/var），子程序看到的是解析後的路徑，只比對資料夾名稱
        Assert.EndsWith(
            Path.GetFileName(_cwd.Path),
            Path.TrimEndingDirectorySeparator(report.GetProperty("cwd").GetString()!),
            StringComparison.OrdinalIgnoreCase);
        var env = report.GetProperty("env");
        Assert.Equal("sdk-py", env.GetProperty("CLAUDE_CODE_ENTRYPOINT").GetString());
        Assert.Equal("0.2.174", env.GetProperty("CLAUDE_AGENT_SDK_VERSION").GetString());
        Assert.Equal("1", env.GetProperty("CLAUDE_CODE_SDK_READS_SESSION_STATE").GetString());
        Assert.Equal(JsonValueKind.Null, env.GetProperty("CLAUDECODE").ValueKind);
        Assert.Equal(_cwd.Path, env.GetProperty("PWD").GetString());
        Assert.Equal("額外的值", env.GetProperty("FAKE_CLAUDE_EXTRA").GetString());
    }

    [Fact]
    public async Task WriteAsync_Utf8TextRoundTripsThroughProcess()
    {
        const string line = """{"type":"user","message":{"role":"user","content":"你好，世界 🐱"},"parent_tool_use_id":null,"session_id":"default"}""";
        await using var transport = new SubprocessTransport(Options("echo"));
        await transport.ConnectAsync();

        await transport.WriteAsync(line + "\n");
        await transport.EndInputAsync();
        var messages = await ReadAllAsync(transport);

        Assert.Equal(2, messages.Count);
        Assert.Equal(line, messages[1].GetRawText());
    }

    [Fact]
    public async Task WriteAsync_ConcurrentWrites_AreNotInterleaved()
    {
        var lines = Enumerable.Range(0, 50)
            .Select(i => $$"""{"type":"control_response","index":{{i}},"padding":"{{new string('長', 2000)}}"}""")
            .ToList();
        await using var transport = new SubprocessTransport(Options("echo"));
        await transport.ConnectAsync();

        // 邊寫邊讀：假 CLI 回寫的資料沒人讀，stdout 管線塞滿後它就不再讀 stdin，兩邊會互等
        var reading = ReadAllAsync(transport);
        await Task.WhenAll(lines.Select(line => Task.Run(() => transport.WriteAsync(line + "\n"))));
        await transport.EndInputAsync();
        var echoed = (await reading).Skip(1).ToList();

        Assert.Equal(
            Enumerable.Range(0, 50),
            echoed.Select(message => message.GetProperty("index").GetInt32()).Order());
    }

    [Fact]
    public async Task ReadMessagesAsync_NonZeroExit_ThrowsCliProcessException()
    {
        await using var transport = new SubprocessTransport(Options("exit"));
        await transport.ConnectAsync();
        List<JsonElement> received = [];

        var error = await Assert.ThrowsAsync<CliProcessException>(async () =>
        {
            await foreach (var message in transport.ReadMessagesAsync())
            {
                received.Add(message);
            }
        });

        Assert.Equal(3, error.ExitCode);
        Assert.Equal("bye", Assert.Single(received).GetProperty("type").GetString());
    }

    [Fact]
    public async Task Stderr_CallbackReceivesEachNonEmptyLine()
    {
        var lines = new ConcurrentQueue<string>();
        var transport = new SubprocessTransport(Options("stderr", lines.Enqueue));
        await transport.ConnectAsync();

        await ReadAllAsync(transport);
        await transport.DisposeAsync();

        Assert.Equal(["第一行錯誤", "第二行錯誤"], lines);
    }

    [Fact]
    public async Task Stderr_CallbackThrowing_DoesNotStopLaterLines()
    {
        var lines = new ConcurrentQueue<string>();
        var transport = new SubprocessTransport(Options("stderr", line =>
        {
            lines.Enqueue(line);
            throw new InvalidOperationException("callback 出錯");
        }));
        await transport.ConnectAsync();

        await ReadAllAsync(transport);
        await transport.DisposeAsync();

        Assert.Equal(2, lines.Count);
    }

    [Fact]
    public async Task DisposeAsync_CliIgnoringEndOfInput_IsKilledAfterGracePeriod()
    {
        var transport = new SubprocessTransport(Options("hang")) { ShutdownGracePeriod = TimeSpan.FromMilliseconds(300) };
        await transport.ConnectAsync();
        await using (var messages = transport.ReadMessagesAsync().GetAsyncEnumerator())
        {
            // 等它開始執行
            Assert.True(await messages.MoveNextAsync());
        }

        var processId = transport.ProcessId!.Value;
        var stopwatch = Stopwatch.StartNew();

        await transport.DisposeAsync();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"收尾花了 {stopwatch.Elapsed}");
        Assert.True(HasExited(processId));
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_IsHarmless()
    {
        var transport = new SubprocessTransport(Options("echo"));
        await transport.ConnectAsync();

        await transport.DisposeAsync();
        await transport.DisposeAsync();

        Assert.False(transport.IsReady);
    }

    [Fact]
    public async Task WriteAsync_AfterDispose_ThrowsCliConnectionException()
    {
        var transport = new SubprocessTransport(Options("echo"));
        await transport.ConnectAsync();
        await transport.DisposeAsync();

        await Assert.ThrowsAsync<CliConnectionException>(() => transport.WriteAsync("{}\n"));
    }

    [Fact]
    public async Task WriteAsync_BeforeConnect_ThrowsCliConnectionException()
    {
        await using var transport = new SubprocessTransport(Options("echo"));

        await Assert.ThrowsAsync<CliConnectionException>(() => transport.WriteAsync("{}\n"));
    }

    [Fact]
    public async Task ConnectAsync_MissingWorkingDirectory_ThrowsCliConnectionException()
    {
        var options = new ClaudeAgentOptions { CliPath = FakeClaude.Path, WorkingDirectory = Path.Combine(_cwd.Path, "沒有這個資料夾") };
        await using var transport = new SubprocessTransport(options);

        var error = await Assert.ThrowsAsync<CliConnectionException>(() => transport.ConnectAsync());
        Assert.Contains("工作目錄不存在", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConnectAsync_MissingExecutable_ThrowsCliNotFoundException()
    {
        var options = new ClaudeAgentOptions { CliPath = Path.Combine(_cwd.Path, "claude.exe"), WorkingDirectory = _cwd.Path };
        await using var transport = new SubprocessTransport(options);

        await Assert.ThrowsAsync<CliNotFoundException>(() => transport.ConnectAsync());
    }

    [Fact]
    public async Task ConnectAsync_BatchFileOnWindows_IsRefusedBeforeStarting()
    {
        var options = new ClaudeAgentOptions { CliPath = _cwd.CreateFile("claude.cmd"), WorkingDirectory = _cwd.Path };
        var locator = new CliLocator(pathVariable: null, _cwd.Path, isWindows: true);
        await using var transport = new SubprocessTransport(options, locator);

        // 確切是 CliConnectionException，不是子類別 CliNotFoundException
        var error = await Assert.ThrowsAsync<CliConnectionException>(() => transport.ConnectAsync());
        Assert.Contains("批次檔", error.Message, StringComparison.Ordinal);
        Assert.Null(transport.ProcessId);
    }

    [Fact]
    public async Task ConnectAsync_CliOlderThanMinimum_WarnsAndStillConnects()
    {
        List<string> warnings = [];
        var options = Options("echo", env: new() { ["FAKE_CLAUDE_VERSION"] = "1.0.128 (Claude Code)" }, warning: warnings.Add);
        await using var transport = new SubprocessTransport(options);

        await transport.ConnectAsync();

        var warning = Assert.Single(warnings);
        Assert.Contains("1.0.128", warning, StringComparison.Ordinal);
        Assert.Contains("2.0.0", warning, StringComparison.Ordinal);
        Assert.Contains(FakeClaude.Path, warning, StringComparison.Ordinal);
        Assert.True(transport.IsReady);
    }

    [Theory]
    [InlineData("2.0.0 (Claude Code)")]
    [InlineData("2.1.309 (Claude Code)")]
    [InlineData("看不懂的輸出")]
    public async Task ConnectAsync_SupportedOrUnreadableVersion_DoesNotWarn(string version)
    {
        List<string> warnings = [];
        var options = Options("echo", env: new() { ["FAKE_CLAUDE_VERSION"] = version }, warning: warnings.Add);
        await using var transport = new SubprocessTransport(options);

        await transport.ConnectAsync();

        Assert.Empty(warnings);
        Assert.True(transport.IsReady);
    }

    [Fact]
    public async Task ConnectAsync_VersionCheckTimesOut_ConnectsWithoutWarning()
    {
        List<string> warnings = [];
        var options = Options("echo", env: new() { ["FAKE_CLAUDE_VERSION"] = "hang" }, warning: warnings.Add);
        await using var transport = new SubprocessTransport(options) { VersionCheckTimeout = TimeSpan.FromMilliseconds(200) };

        await transport.ConnectAsync();

        Assert.Empty(warnings);
        Assert.True(transport.IsReady);
    }

    private ClaudeAgentOptions Options(
        string mode,
        Action<string>? stderr = null,
        Dictionary<string, string>? env = null,
        Action<string>? warning = null)
    {
        var allEnv = env ?? [];
        allEnv["FAKE_CLAUDE_MODE"] = mode;
        return new ClaudeAgentOptions
        {
            CliPath = FakeClaude.Path,
            WorkingDirectory = _cwd.Path,
            Model = "haiku",
            ResumeSessionId = "34953276-f9da-4b8c-a789-77d8ee05bc37",
            IncludePartialMessages = true,
            DisallowedTools = ["AskUserQuestion"],
            MaxTurns = 3,
            MaxBudgetUsd = 0.5m,
            // schema 的 JSON 也要整個當成一個參數送到子程序
            OutputSchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject { ["回答"] = new JsonObject { ["type"] = "string", ["description"] = "簡短 & 直接" } },
            },
            // 引號、空白、& 都要原樣送到子程序，Windows 也是
            ExtraArguments = new Dictionary<string, string?>
            {
                ["mcp-config"] = """{"mcpServers": {"紫苑": {"command": "node", "args": ["a & b"]}}}""",
                ["strict-mcp-config"] = null,
            },
            EnvironmentVariables = allEnv,
            StandardErrorCallback = stderr,
            WarningCallback = warning,
        };
    }

    private static async Task<List<JsonElement>> ReadAllAsync(SubprocessTransport transport)
    {
        List<JsonElement> messages = [];
        await foreach (var message in transport.ReadMessagesAsync())
        {
            messages.Add(message);
        }

        return messages;
    }

    private static bool HasExited(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }
}
