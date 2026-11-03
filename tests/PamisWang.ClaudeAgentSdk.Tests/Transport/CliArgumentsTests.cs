using System.Globalization;
using System.Text.Json.Nodes;
using PamisWang.ClaudeAgentSdk.Mcp;
using PamisWang.ClaudeAgentSdk.Permissions;
using PamisWang.ClaudeAgentSdk.Transport;

namespace PamisWang.ClaudeAgentSdk.Tests.Transport;

public sealed class CliArgumentsTests
{
    [Fact]
    public void Build_Defaults_MatchesPythonSdkBaseCommand()
    {
        var args = CliArguments.Build(new ClaudeAgentOptions(), isWindows: false);

        Assert.Equal(
            ["--output-format", "stream-json", "--verbose", "--system-prompt", "", "--input-format", "stream-json"],
            args);
    }

    [Fact]
    public void Build_ChatOptions_AddsFlagsInPythonSdkOrder()
    {
        var options = new ClaudeAgentOptions
        {
            Model = "haiku",
            ResumeSessionId = "34953276-f9da-4b8c-a789-77d8ee05bc37",
            IncludePartialMessages = true,
            DisallowedTools = ["AskUserQuestion", "Bash"],
        };

        var args = CliArguments.Build(options, isWindows: true);

        Assert.Equal(
            [
                "--output-format", "stream-json", "--verbose", "--system-prompt", "",
                "--disallowedTools", "AskUserQuestion,Bash",
                "--model", "haiku",
                "--resume=34953276-f9da-4b8c-a789-77d8ee05bc37",
                "--include-partial-messages",
                "--input-format", "stream-json",
            ],
            args);
    }

    [Fact]
    public void Build_WithCanUseTool_RoutesPermissionPromptsOverStdio()
    {
        var options = new ClaudeAgentOptions
        {
            Model = "haiku",
            ResumeSessionId = "abc",
            CanUseTool = (_, _) => Task.FromResult<PermissionResult>(new PermissionResultAllow()),
        };

        var args = CliArguments.Build(options, isWindows: false);

        // 跟 Python SDK 一樣排在 --model 之後、--resume 之前
        Assert.Equal(
            [
                "--output-format", "stream-json", "--verbose", "--system-prompt", "",
                "--model", "haiku",
                "--permission-prompt-tool", "stdio",
                "--resume=abc",
                "--input-format", "stream-json",
            ],
            args);
    }

    [Fact]
    public void Build_SystemPromptPreset_LeavesClaudeCodeDefault()
    {
        var args = CliArguments.Build(new ClaudeAgentOptions { SystemPrompt = new SystemPromptPreset() }, isWindows: false);

        Assert.Equal(["--output-format", "stream-json", "--verbose", "--input-format", "stream-json"], args);
    }

    [Fact]
    public void Build_SystemPromptPresetWithAppend_AppendsToDefault()
    {
        var options = new ClaudeAgentOptions { SystemPrompt = new SystemPromptPreset(Append: "回答一律用繁體中文") };

        var args = CliArguments.Build(options, isWindows: false);

        Assert.Equal(
            ["--output-format", "stream-json", "--verbose", "--append-system-prompt", "回答一律用繁體中文", "--input-format", "stream-json"],
            args);
    }

    [Fact]
    public void Build_SystemPromptCustom_ReplacesDefaultAsOneArgument()
    {
        // 換行、引號、- 開頭都原樣放在一個參數裡；不經過 cmd.exe，Windows 上也不擋
        const string prompt = "- 你是紫苑\n- 說話\"溫柔\" & 簡短";
        var options = new ClaudeAgentOptions { SystemPrompt = new SystemPromptCustom(prompt), Model = "haiku" };

        var args = CliArguments.Build(options, isWindows: true);

        Assert.Equal(
            [
                "--output-format", "stream-json", "--verbose", "--system-prompt", prompt,
                "--model", "haiku",
                "--input-format", "stream-json",
            ],
            args);
    }

    [Fact]
    public void Build_SystemPromptFile_PassesPath()
    {
        var options = new ClaudeAgentOptions { SystemPrompt = new SystemPromptFile(@"D:\prompts\紫苑.md") };

        var args = CliArguments.Build(options, isWindows: true);

        Assert.Equal(
            ["--output-format", "stream-json", "--verbose", "--system-prompt-file", @"D:\prompts\紫苑.md", "--input-format", "stream-json"],
            args);
    }

    [Fact]
    public void Build_Effort_ComesAfterPartialMessagesLikePythonSdk()
    {
        var options = new ClaudeAgentOptions { Model = "haiku", Effort = "low", IncludePartialMessages = true };

        var args = CliArguments.Build(options, isWindows: false);

        Assert.Equal(
            [
                "--output-format", "stream-json", "--verbose", "--system-prompt", "",
                "--model", "haiku",
                "--include-partial-messages",
                "--effort", "low",
                "--input-format", "stream-json",
            ],
            args);
    }

    [Fact]
    public void Build_Limits_ComeBeforeDisallowedToolsLikePythonSdk()
    {
        var options = new ClaudeAgentOptions
        {
            Model = "haiku",
            MaxTurns = 3,
            MaxBudgetUsd = 0.5m,
            DisallowedTools = ["Bash"],
        };

        var args = CliArguments.Build(options, isWindows: false);

        Assert.Equal(
            [
                "--output-format", "stream-json", "--verbose", "--system-prompt", "",
                "--max-turns", "3",
                "--max-budget-usd", "0.5",
                "--disallowedTools", "Bash",
                "--model", "haiku",
                "--input-format", "stream-json",
            ],
            args);
    }

    [Fact]
    public void Build_MaxBudgetUsd_IgnoresCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // 德文的小數點是逗號，照目前語系寫會送出 1,25
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            var args = CliArguments.Build(new ClaudeAgentOptions { MaxBudgetUsd = 1.25m }, isWindows: false);

            Assert.Equal("1.25", args[args.ToList().IndexOf("--max-budget-usd") + 1]);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Build_NonPositiveMaxTurns_Throws(int maxTurns)
    {
        // Python 遇到 0 會略過這個旗標，負數則送出 - 開頭、會被當成另一個旗標的值
        var options = new ClaudeAgentOptions { MaxTurns = maxTurns };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CliArguments.Build(options, isWindows: false));
        Assert.Equal(nameof(ClaudeAgentOptions.MaxTurns), exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    public void Build_NonPositiveMaxBudgetUsd_Throws(double maxBudgetUsd)
    {
        var options = new ClaudeAgentOptions { MaxBudgetUsd = (decimal)maxBudgetUsd };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CliArguments.Build(options, isWindows: false));
        Assert.Equal(nameof(ClaudeAgentOptions.MaxBudgetUsd), exception.ParamName);
    }

    [Fact]
    public void Build_OutputSchema_ComesAfterEffortAsOneJsonArgument()
    {
        var options = new ClaudeAgentOptions
        {
            Effort = "low",
            OutputSchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject { ["answer"] = new JsonObject { ["type"] = "string" } },
                ["required"] = new JsonArray("answer"),
            },
        };

        var args = CliArguments.Build(options, isWindows: true);

        Assert.Equal(
            [
                "--output-format", "stream-json", "--verbose", "--system-prompt", "",
                "--effort", "low",
                "--json-schema", """{"type":"object","properties":{"answer":{"type":"string"}},"required":["answer"]}""",
                "--input-format", "stream-json",
            ],
            args);
    }

    [Fact]
    public void Build_ExtraArguments_ComeBeforeEffortLikePythonSdk()
    {
        var options = new ClaudeAgentOptions
        {
            IncludePartialMessages = true,
            Effort = "low",
            ExtraArguments = new Dictionary<string, string?>
            {
                ["max-turns"] = "3",
                ["strict-mcp-config"] = null,
                ["add-dir"] = @"D:\work\shared",
            },
        };

        var args = CliArguments.Build(options, isWindows: false);

        Assert.Equal(
            [
                "--output-format", "stream-json", "--verbose", "--system-prompt", "",
                "--include-partial-messages",
                "--max-turns", "3",
                "--strict-mcp-config",
                "--add-dir", @"D:\work\shared",
                "--effort", "low",
                "--input-format", "stream-json",
            ],
            args);
    }

    [Fact]
    public void Build_ExtraArgumentValueStartingWithDash_UsesEqualsForm()
    {
        // 跟 Python 一樣：分成兩個參數的話，--evil 會被 CLI 當成另一個旗標
        var options = new ClaudeAgentOptions { ExtraArguments = new Dictionary<string, string?> { ["future-flag"] = "--evil" } };

        var args = CliArguments.Build(options, isWindows: false);

        Assert.Contains("--future-flag=--evil", args);
        Assert.DoesNotContain("--evil", args);
        Assert.DoesNotContain("--future-flag", args);
    }

    [Fact]
    public void Build_OnWindows_ExtraArgumentJsonValueStaysOneArgument()
    {
        // JSON 一定有引號；不經過 cmd.exe，Windows 上也不擋 ResumeSessionId 擋的那些字元
        const string config = """{"mcpServers": {"紫苑": {"command": "node", "args": ["a & b"]}}}""";
        var options = new ClaudeAgentOptions { ExtraArguments = new Dictionary<string, string?> { ["mcp-config"] = config } };

        var args = CliArguments.Build(options, isWindows: true);

        Assert.Equal(config, args[args.ToList().IndexOf("--mcp-config") + 1]);
    }

    [Fact]
    public void Build_ExtraArgumentEmptyValue_IsPassedAsEmptyArgument()
    {
        var options = new ClaudeAgentOptions { ExtraArguments = new Dictionary<string, string?> { ["tools"] = "" } };

        var args = CliArguments.Build(options, isWindows: false);

        Assert.Equal("", args[args.ToList().IndexOf("--tools") + 1]);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("--max-turns")]
    [InlineData("-p")]
    [InlineData("max-turns=3")]
    [InlineData("output-format")]
    [InlineData("input-format")]
    public void Build_InvalidExtraArgumentFlag_Throws(string flag)
    {
        var options = new ClaudeAgentOptions { ExtraArguments = new Dictionary<string, string?> { [flag] = "x" } };

        var exception = Assert.Throws<ArgumentException>(() => CliArguments.Build(options, isWindows: false));
        Assert.Equal(nameof(ClaudeAgentOptions.ExtraArguments), exception.ParamName);
    }

    [Fact]
    public void Build_PermissionPromptToolInExtraArguments_IsRejectedWithCanUseTool()
    {
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (_, _) => Task.FromResult<PermissionResult>(new PermissionResultAllow()),
            ExtraArguments = new Dictionary<string, string?> { ["permission-prompt-tool"] = "mcp__approver__ask" },
        };

        Assert.Throws<ArgumentException>(() => CliArguments.Build(options, isWindows: false));
    }

    [Fact]
    public void Build_PermissionPromptToolInExtraArguments_IsAllowedWithoutCanUseTool()
    {
        var options = new ClaudeAgentOptions
        {
            ExtraArguments = new Dictionary<string, string?> { ["permission-prompt-tool"] = "mcp__approver__ask" },
        };

        var args = CliArguments.Build(options, isWindows: false);

        Assert.Equal("mcp__approver__ask", args[args.ToList().IndexOf("--permission-prompt-tool") + 1]);
    }

    [Fact]
    public void Build_ResumeSessionIdStartingWithDash_StaysOneArgument()
    {
        // 分成兩個參數的話，CLI 會把 --model 當成另一個旗標
        var args = CliArguments.Build(new ClaudeAgentOptions { ResumeSessionId = "--model" }, isWindows: false);

        Assert.Contains("--resume=--model", args);
        Assert.DoesNotContain("--model", args);
    }

    [Theory]
    [InlineData("a&b")]
    [InlineData("a|b")]
    [InlineData("a>b")]
    [InlineData("a^b")]
    [InlineData("%PATH%")]
    [InlineData("a!b")]
    [InlineData("a\"b")]
    [InlineData("a\nb")]
    public void Build_OnWindows_RejectsCmdMetacharactersInResumeSessionId(string resume)
    {
        var options = new ClaudeAgentOptions { ResumeSessionId = resume };

        Assert.Throws<ArgumentException>(() => CliArguments.Build(options, isWindows: true));
    }

    [Fact]
    public void Build_OnWindows_AcceptsOrdinarySessionTitle()
    {
        var args = CliArguments.Build(new ClaudeAgentOptions { ResumeSessionId = "重構 server (v2)" }, isWindows: true);

        Assert.Contains("--resume=重構 server (v2)", args);
    }

    [Fact]
    public void Build_OffWindows_AllowsAnyResumeSessionId()
    {
        var args = CliArguments.Build(new ClaudeAgentOptions { ResumeSessionId = "a&b" }, isWindows: false);

        Assert.Contains("--resume=a&b", args);
    }

    [Fact]
    public void Build_EmptyResumeSessionIdAndModel_AreOmitted()
    {
        var args = CliArguments.Build(new ClaudeAgentOptions { ResumeSessionId = "", Model = "" }, isWindows: false);

        Assert.DoesNotContain(args, arg => arg.StartsWith("--resume", StringComparison.Ordinal));
        Assert.DoesNotContain("--model", args);
    }

    [Fact]
    public void Build_SessionAndToolOptions_FollowPythonOrderWithTypeScriptOnlyFlagsNearby()
    {
        var options = new ClaudeAgentOptions
        {
            Tools = ["Read", "Bash"],
            AllowedTools = ["Read"],
            Skills = SkillSelection.Only("pdf"),
            MaxTurns = 3,
            DisallowedTools = ["WebFetch"],
            TaskBudget = 5000,
            Model = "haiku",
            Agent = "reviewer",
            PermissionPromptToolName = "mcp__approver__ask",
            PermissionMode = PermissionMode.AcceptEdits,
            AllowDangerouslySkipPermissions = true,
            ResumeSessionId = "abc",
            SessionId = "0b5c6a52-6bd6-4c5e-9a0b-6a37c4e7f2d1",
            ForkSession = true,
            ResumeSessionAt = "u1",
            ResumeDropsTurn = "p1",
            Settings = new JsonObject { ["cleanupPeriodDays"] = 30 },
            Sandbox = new JsonObject { ["enabled"] = true },
            ManagedSettings = new JsonObject { ["allowManagedHooksOnly"] = true },
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["files"] = new McpStdioServerConfig("node") { Args = ["server.js"], Env = new Dictionary<string, string> { ["TOKEN"] = "t" } },
            },
            IncludePartialMessages = true,
            StrictMcpConfig = true,
            PersistSession = false,
            SettingSources = [SettingSource.User, SettingSource.Project],
            Plugins = [new LocalPlugin("plugins/a"), new LocalPlugin("plugins/b") { SkipMcpDiscovery = true }],
            ExtraArguments = new Dictionary<string, string?> { ["debug"] = null },
            Effort = "high",
        };

        var args = CliArguments.Build(options, isWindows: true);

        Assert.Equal(
            [
                "--output-format", "stream-json", "--verbose", "--system-prompt", "",
                "--tools", "Read,Bash",
                "--allowedTools", "Read,Skill(pdf)",
                "--max-turns", "3",
                "--disallowedTools", "WebFetch",
                "--task-budget", "5000",
                "--model", "haiku",
                "--agent", "reviewer",
                "--permission-prompt-tool", "mcp__approver__ask",
                "--permission-mode", "acceptEdits",
                "--allow-dangerously-skip-permissions",
                "--resume=abc",
                "--session-id=0b5c6a52-6bd6-4c5e-9a0b-6a37c4e7f2d1",
                "--settings", """{"cleanupPeriodDays":30,"sandbox":{"enabled":true}}""",
                "--managed-settings", """{"allowManagedHooksOnly":true}""",
                "--mcp-config", """{"mcpServers":{"files":{"type":"stdio","command":"node","args":["server.js"],"env":{"TOKEN":"t"}}}}""",
                "--include-partial-messages",
                "--strict-mcp-config",
                "--fork-session",
                "--resume-session-at=u1",
                "--resume-drops-turn=p1",
                "--no-session-persistence",
                "--setting-sources=user,project",
                "--plugin-dir", "plugins/a",
                "--plugin-dir-no-mcp", "plugins/b",
                "--debug",
                "--effort", "high",
                "--input-format", "stream-json",
            ],
            args);
        // 併入 sandbox 時不改呼叫端的物件
        Assert.False(options.Settings.ContainsKey("sandbox"));
    }

    [Fact]
    public void Build_EmptyTools_DisablesAllBuiltInToolsWithEmptyValue()
    {
        var args = CliArguments.Build(new ClaudeAgentOptions { Tools = [] }, isWindows: false);

        Assert.Equal("", args[args.ToList().IndexOf("--tools") + 1]);
    }

    [Fact]
    public void Build_EmptySettingSources_LoadsNoSettingsFiles()
    {
        var args = CliArguments.Build(new ClaudeAgentOptions { SettingSources = [] }, isWindows: false);

        Assert.Contains("--setting-sources=", args);
    }

    [Fact]
    public void Build_ContinueAndPermissionPromptsNone_AddBareFlags()
    {
        var options = new ClaudeAgentOptions { ContinueConversation = true, DisablePermissionPrompts = true };

        var args = CliArguments.Build(options, isWindows: false);

        Assert.Contains("--continue", args);
        Assert.Equal("none", args[args.ToList().IndexOf("--permission-prompts") + 1]);
    }

    [Fact]
    public void Build_SettingsFile_PassesPath()
    {
        var args = CliArguments.Build(new ClaudeAgentOptions { SettingsFile = "ci-settings.json" }, isWindows: false);

        Assert.Equal("ci-settings.json", args[args.ToList().IndexOf("--settings") + 1]);
    }

    [Fact]
    public void Build_SandboxWithoutSettings_BecomesSettingsSandbox()
    {
        var args = CliArguments.Build(new ClaudeAgentOptions { Sandbox = new JsonObject { ["enabled"] = true } }, isWindows: false);

        Assert.Equal("""{"sandbox":{"enabled":true}}""", args[args.ToList().IndexOf("--settings") + 1]);
    }

    [Fact]
    public void Build_AllSkills_AddsSkillRuleOnce()
    {
        var options = new ClaudeAgentOptions { AllowedTools = ["Skill"], Skills = SkillSelection.All };

        var args = CliArguments.Build(options, isWindows: false);

        Assert.Equal("Skill", args[args.ToList().IndexOf("--allowedTools") + 1]);
    }

    [Fact]
    public void Build_PluginsDeliveredInInitialize_AwaitInitializeInsteadOfPluginDirs()
    {
        var options = new ClaudeAgentOptions { Plugins = [new LocalPlugin("plugins/a")], PluginDelivery = PluginDelivery.Initialize };

        var args = CliArguments.Build(options, isWindows: false);

        Assert.Contains("--await-initialize", args);
        Assert.DoesNotContain("--plugin-dir", args);
    }

    [Fact]
    public void Build_PermissionModeNotListedYet_PassesValueThrough()
    {
        var args = CliArguments.Build(new ClaudeAgentOptions { PermissionMode = new PermissionMode("manual") }, isWindows: false);

        Assert.Equal("manual", args[args.ToList().IndexOf("--permission-mode") + 1]);
    }

    [Fact]
    public void Build_PermissionPromptToolInExtraArguments_IsRejectedWithPermissionPromptToolName()
    {
        var options = new ClaudeAgentOptions
        {
            PermissionPromptToolName = "mcp__approver__ask",
            ExtraArguments = new Dictionary<string, string?> { ["permission-prompt-tool"] = "mcp__other__ask" },
        };

        var exception = Assert.Throws<ArgumentException>(() => CliArguments.Build(options, isWindows: false));
        Assert.Equal(nameof(ClaudeAgentOptions.ExtraArguments), exception.ParamName);
    }

    [Theory]
    [InlineData(nameof(ClaudeAgentOptions.ContinueConversation))]
    [InlineData(nameof(ClaudeAgentOptions.ForkSession))]
    [InlineData(nameof(ClaudeAgentOptions.SessionId))]
    [InlineData(nameof(ClaudeAgentOptions.ResumeSessionAt))]
    [InlineData(nameof(ClaudeAgentOptions.ResumeDropsTurn))]
    [InlineData(nameof(ClaudeAgentOptions.PermissionPromptToolName))]
    [InlineData(nameof(ClaudeAgentOptions.DisablePermissionPrompts))]
    [InlineData(nameof(ClaudeAgentOptions.SettingsFile))]
    [InlineData(nameof(ClaudeAgentOptions.Sandbox))]
    public void Build_ConflictingOptions_ThrowNamingTheOption(string option)
    {
        var exception = Assert.Throws<ArgumentException>(() => CliArguments.Build(ConflictingOptions(option), isWindows: false));

        Assert.Equal(option, exception.ParamName);
    }

    [Fact]
    public void Build_SandboxAlongsideSettingsWithItsOwnSandbox_Throws()
    {
        var options = new ClaudeAgentOptions
        {
            Settings = new JsonObject { ["sandbox"] = new JsonObject { ["enabled"] = false } },
            Sandbox = new JsonObject { ["enabled"] = true },
        };

        var exception = Assert.Throws<ArgumentException>(() => CliArguments.Build(options, isWindows: false));
        Assert.Equal(nameof(ClaudeAgentOptions.Sandbox), exception.ParamName);
    }

    [Fact]
    public void Build_SessionIdWithResumeAndFork_IsAllowed()
    {
        var options = new ClaudeAgentOptions { ResumeSessionId = "abc", ForkSession = true, SessionId = "new-id" };

        var args = CliArguments.Build(options, isWindows: false);

        Assert.Contains("--session-id=new-id", args);
        Assert.Contains("--fork-session", args);
    }

    [Theory]
    [InlineData(nameof(ClaudeAgentOptions.SessionId))]
    [InlineData(nameof(ClaudeAgentOptions.ResumeSessionAt))]
    [InlineData(nameof(ClaudeAgentOptions.ResumeDropsTurn))]
    public void Build_OnWindows_RejectsCmdMetacharactersInSessionValues(string option)
    {
        var options = option switch
        {
            nameof(ClaudeAgentOptions.SessionId) => new ClaudeAgentOptions { SessionId = "a&b" },
            nameof(ClaudeAgentOptions.ResumeSessionAt) => new ClaudeAgentOptions { ResumeSessionId = "abc", ResumeSessionAt = "a&b" },
            _ => new ClaudeAgentOptions { ResumeSessionId = "abc", ResumeSessionAt = "u1", ResumeDropsTurn = "a&b" },
        };

        var exception = Assert.Throws<ArgumentException>(() => CliArguments.Build(options, isWindows: true));
        Assert.Equal(option, exception.ParamName);
    }

    [Fact]
    public void Build_EmptyResumeDropsTurn_IsStillSentSoCliRejectsIt()
    {
        var options = new ClaudeAgentOptions { ResumeSessionId = "abc", ResumeSessionAt = "u1", ResumeDropsTurn = "" };

        var args = CliArguments.Build(options, isWindows: false);

        Assert.Contains("--resume-drops-turn=", args);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Build_NonPositiveTaskBudget_Throws(int taskBudget)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => CliArguments.Build(new ClaudeAgentOptions { TaskBudget = taskBudget }, isWindows: false));
        Assert.Equal(nameof(ClaudeAgentOptions.TaskBudget), exception.ParamName);
    }

    private static ClaudeAgentOptions ConflictingOptions(string option) => option switch
    {
        nameof(ClaudeAgentOptions.ContinueConversation) => new() { ContinueConversation = true, ResumeSessionId = "abc" },
        nameof(ClaudeAgentOptions.ForkSession) => new() { ForkSession = true },
        nameof(ClaudeAgentOptions.SessionId) => new() { SessionId = "new-id", ResumeSessionId = "abc" },
        nameof(ClaudeAgentOptions.ResumeSessionAt) => new() { ResumeSessionAt = "u1" },
        nameof(ClaudeAgentOptions.ResumeDropsTurn) => new() { ResumeSessionId = "abc", ResumeDropsTurn = "p1" },
        nameof(ClaudeAgentOptions.PermissionPromptToolName) => new()
        {
            PermissionPromptToolName = "mcp__approver__ask",
            CanUseTool = (_, _) => Task.FromResult<PermissionResult>(new PermissionResultAllow()),
        },
        nameof(ClaudeAgentOptions.DisablePermissionPrompts) => new()
        {
            DisablePermissionPrompts = true,
            CanUseTool = (_, _) => Task.FromResult<PermissionResult>(new PermissionResultAllow()),
        },
        nameof(ClaudeAgentOptions.SettingsFile) => new() { SettingsFile = "a.json", Settings = [] },
        nameof(ClaudeAgentOptions.Sandbox) => new() { SettingsFile = "a.json", Sandbox = [] },
        _ => throw new ArgumentOutOfRangeException(nameof(option), option, null),
    };
}
