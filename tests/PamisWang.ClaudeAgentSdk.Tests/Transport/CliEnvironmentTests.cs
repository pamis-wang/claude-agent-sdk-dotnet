using PamisWang.ClaudeAgentSdk.Transport;

namespace PamisWang.ClaudeAgentSdk.Tests.Transport;

public sealed class CliEnvironmentTests
{
    [Fact]
    public void Apply_IdentifiesAsPythonSdkProtocolAndThisPackage()
    {
        var env = Apply(new ClaudeAgentOptions());

        Assert.Equal("sdk-py", env["CLAUDE_CODE_ENTRYPOINT"]);
        Assert.Equal("0.2.174", env["CLAUDE_AGENT_SDK_VERSION"]);
        Assert.Equal($"PamisWang.ClaudeAgentSdk/{SdkInfo.Version}", env["CLAUDE_AGENT_SDK_CLIENT_APP"]);
        Assert.DoesNotContain('+', SdkInfo.Version);
    }

    [Fact]
    public void Apply_RemovesInheritedClaudeCode()
    {
        var env = Apply(new ClaudeAgentOptions(), new() { ["CLAUDECODE"] = "1", ["HOME"] = "/home/me" });

        Assert.False(env.ContainsKey("CLAUDECODE"));
        Assert.Equal("/home/me", env["HOME"]);
    }

    [Fact]
    public void Apply_CallerCanSetClaudeCodeBack()
    {
        var options = new ClaudeAgentOptions { EnvironmentVariables = new Dictionary<string, string> { ["CLAUDECODE"] = "1" } };

        var env = Apply(options, new() { ["CLAUDECODE"] = "inherited" });

        Assert.Equal("1", env["CLAUDECODE"]);
    }

    [Fact]
    public void Apply_CallerEnvOverridesEntrypointAndClientApp()
    {
        var options = new ClaudeAgentOptions
        {
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["CLAUDE_CODE_ENTRYPOINT"] = "sdk-ts",
                ["CLAUDE_AGENT_SDK_CLIENT_APP"] = "my-app/1.0",
            },
        };

        var env = Apply(options);

        Assert.Equal("sdk-ts", env["CLAUDE_CODE_ENTRYPOINT"]);
        Assert.Equal("my-app/1.0", env["CLAUDE_AGENT_SDK_CLIENT_APP"]);
    }

    [Fact]
    public void Apply_CallerCannotOverrideSdkVersion()
    {
        var options = new ClaudeAgentOptions
        {
            EnvironmentVariables = new Dictionary<string, string> { ["CLAUDE_AGENT_SDK_VERSION"] = "9.9.9" },
        };

        var env = Apply(options);

        Assert.Equal("0.2.174", env["CLAUDE_AGENT_SDK_VERSION"]);
    }

    [Fact]
    public void Apply_ReadsSessionStateByDefault()
    {
        Assert.Equal("1", Apply(new ClaudeAgentOptions())["CLAUDE_CODE_SDK_READS_SESSION_STATE"]);
    }

    [Fact]
    public void Apply_CallerChoseSessionState_IsKept()
    {
        var options = new ClaudeAgentOptions
        {
            EnvironmentVariables = new Dictionary<string, string> { ["CLAUDE_CODE_SDK_READS_SESSION_STATE"] = "0" },
        };

        Assert.Equal("0", Apply(options)["CLAUDE_CODE_SDK_READS_SESSION_STATE"]);
    }

    [Fact]
    public void Apply_InheritedSessionStateInOtherCase_IsNotDuplicated()
    {
        var env = Apply(new ClaudeAgentOptions(), new() { ["claude_code_sdk_reads_session_state"] = "0" });

        Assert.Equal("0", env["claude_code_sdk_reads_session_state"]);
        Assert.False(env.ContainsKey("CLAUDE_CODE_SDK_READS_SESSION_STATE"));
    }

    [Fact]
    public void Apply_WithWorkingDirectory_SetsPwd()
    {
        var env = Apply(new ClaudeAgentOptions { WorkingDirectory = "/work/project" }, new() { ["PWD"] = "/elsewhere" });

        Assert.Equal("/work/project", env["PWD"]);
    }

    [Fact]
    public void Apply_WithoutWorkingDirectory_KeepsInheritedPwd()
    {
        var env = Apply(new ClaudeAgentOptions(), new() { ["PWD"] = "/elsewhere" });

        Assert.Equal("/elsewhere", env["PWD"]);
    }

    [Fact]
    public void Apply_FileCheckpointingAndPreviewFormat_WinOverCallerEnv()
    {
        var options = new ClaudeAgentOptions
        {
            EnableFileCheckpointing = true,
            AskUserQuestionPreviewFormat = "html",
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["CLAUDE_CODE_ENABLE_SDK_FILE_CHECKPOINTING"] = "false",
                ["CLAUDE_CODE_QUESTION_PREVIEW_FORMAT"] = "markdown",
            },
        };

        var env = Apply(options);

        Assert.Equal("true", env["CLAUDE_CODE_ENABLE_SDK_FILE_CHECKPOINTING"]);
        Assert.Equal("html", env["CLAUDE_CODE_QUESTION_PREVIEW_FORMAT"]);
    }

    [Fact]
    public void Apply_WithoutCheckpointingOrPreviewFormat_LeavesThemUnset()
    {
        var env = Apply(new ClaudeAgentOptions());

        Assert.False(env.ContainsKey("CLAUDE_CODE_ENABLE_SDK_FILE_CHECKPOINTING"));
        Assert.False(env.ContainsKey("CLAUDE_CODE_QUESTION_PREVIEW_FORMAT"));
    }

    private static Dictionary<string, string?> Apply(ClaudeAgentOptions options, Dictionary<string, string?>? inherited = null)
    {
        var env = inherited ?? [];
        CliEnvironment.Apply(env, options);
        return env;
    }
}
