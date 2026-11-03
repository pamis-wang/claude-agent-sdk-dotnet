using System.Text.Json;
using PamisWang.ClaudeAgentSdk.Messages;

namespace PamisWang.ClaudeAgentSdk.Tests.Messages;

public sealed class MessageParserTests
{
    [Theory]
    [MemberData(nameof(ProtocolFixture.Names), MemberType = typeof(ProtocolFixture))]
    public void Parse_EveryRecordedMessage_GetsItsTypedClass(string fixture)
    {
        var messages = ProtocolFixture.Load(fixture).Received
            .Where(message => !message.GetProperty("type").GetString()!.StartsWith("control_", StringComparison.Ordinal));

        Assert.All(messages, data =>
        {
            var message = MessageParser.Parse(data);

            // 錄到的每一種型別都有對應的類別；內容區塊也都認得
            Assert.IsNotType<UnknownMessage>(message);
            Assert.True(JsonElement.DeepEquals(data, message.Raw));
            Assert.Equal(data.GetProperty("session_id").GetString(), message.SessionId);
            var blocks = message switch
            {
                AssistantMessage assistant => assistant.Content,
                UserMessage user => user.Content,
                _ => [],
            };
            Assert.All(blocks, block => Assert.IsNotType<UnknownContentBlock>(block));
        });
    }

    [Fact]
    public void Parse_AssistantToolUse_ReadsToolCall()
    {
        var assistant = First<AssistantMessage>("permission_allow", m => m.Content.Any(b => b is ToolUseBlock));

        var tool = Assert.IsType<ToolUseBlock>(Assert.Single(assistant.Content));
        Assert.Equal("Write", tool.Name);
        Assert.StartsWith("toolu_", tool.Id, StringComparison.Ordinal);
        Assert.Equal("你好", tool.Input.GetProperty("content").GetString());
        Assert.Equal(assistant.Raw.GetProperty("message").GetProperty("model").GetString(), assistant.Model);
        Assert.Null(assistant.ParentToolUseId);
        Assert.NotNull(assistant.Usage);
    }

    [Fact]
    public void Parse_AssistantThinking_ReadsThinkingAndSignature()
    {
        var assistant = First<AssistantMessage>("permission_deny", m => m.Content.Any(b => b is ThinkingBlock));

        var thinking = Assert.IsType<ThinkingBlock>(Assert.Single(assistant.Content));
        Assert.NotEmpty(thinking.Signature);
    }

    [Fact]
    public void Parse_UserToolResult_ReadsResultAndErrorFlag()
    {
        var denied = First<UserMessage>("permission_deny", m => m.Content.Any(b => b is ToolResultBlock));

        var result = Assert.IsType<ToolResultBlock>(Assert.Single(denied.Content));
        Assert.True(result.IsError);
        Assert.Equal("使用者在辦公室拒絕了這個動作", result.Content?.GetString());
    }

    [Fact]
    public void Parse_UserStringContent_BecomesOneTextBlock()
    {
        var message = Parse("""{"type":"user","message":{"role":"user","content":"你好"},"parent_tool_use_id":null,"session_id":"s1"}""");

        var user = Assert.IsType<UserMessage>(message);
        Assert.Equal("你好", Assert.IsType<TextBlock>(Assert.Single(user.Content)).Text);
    }

    [Fact]
    public void Parse_SystemInit_ReadsSubtypeAndSession()
    {
        var init = First<SystemMessage>("basic", m => m.Subtype == "init");

        // 樣本重錄後 session ID 與模型的回答都會變，跟原始 JSON 比，不寫死
        Assert.Equal(init.Raw.GetProperty("session_id").GetString(), init.SessionId);
        Assert.Matches("^[0-9a-f-]{36}$", init.SessionId);
    }

    [Fact]
    public void Parse_SuccessResult_ReadsResultCostAndDuration()
    {
        var result = First<ResultMessage>("basic", _ => true);

        Assert.Equal("success", result.Subtype);
        Assert.False(result.IsError);
        Assert.Equal(result.Raw.GetProperty("result").GetString(), result.Result);
        Assert.Contains("玉山", result.Result, StringComparison.Ordinal);
        Assert.Equal(result.Raw.GetProperty("duration_ms").GetInt64(), result.DurationMilliseconds);
        Assert.Equal(result.Raw.GetProperty("total_cost_usd").GetDouble(), result.TotalCostUsd);
        Assert.True(result.TotalCostUsd > 0);
        Assert.Equal(1, result.TurnCount);
        Assert.Equal("completed", result.TerminalReason);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Parse_InterruptedResult_ReadsErrors()
    {
        var result = First<ResultMessage>("interrupt", m => m.IsError);

        Assert.Equal("error_during_execution", result.Subtype);
        Assert.Null(result.Result);
        Assert.Equal("aborted_streaming", result.TerminalReason);
        Assert.Equal(result.Raw.GetProperty("errors")[0].GetString(), Assert.Single(result.Errors));
    }

    [Fact]
    public void Parse_NotLoggedIn_ExposesAuthenticationFailure()
    {
        // CLI 2.1.309 沒登入時送出的兩則訊息，只留相關欄位
        var assistant = Assert.IsType<AssistantMessage>(Parse("""
            {"type":"assistant","message":{"id":"m1","model":"<synthetic>","role":"assistant","stop_reason":"stop_sequence","type":"message",
            "content":[{"type":"text","text":"Not logged in · Please run /login"}]},
            "parent_tool_use_id":null,"session_id":"s1","uuid":"u1","error":"authentication_failed","is_api_error_message":true}
            """));
        var result = Assert.IsType<ResultMessage>(Parse("""
            {"type":"result","subtype":"success","is_error":true,"duration_ms":158,"duration_api_ms":0,"num_turns":1,
            "result":"Not logged in · Please run /login","stop_reason":"stop_sequence","session_id":"s1","total_cost_usd":0,
            "permission_denials":[],"terminal_reason":"api_error","api_error_status":null,"uuid":"u2"}
            """));

        Assert.Equal("authentication_failed", assistant.Error);
        // 沒有 error_ 開頭的 subtype，只有 IsError 與 TerminalReason 看得出出錯；錯誤訊息放在 Result
        Assert.Equal("success", result.Subtype);
        Assert.True(result.IsError);
        Assert.Equal("api_error", result.TerminalReason);
        Assert.Equal("Not logged in · Please run /login", result.Result);
        Assert.Empty(result.Errors);
        Assert.Null(result.ApiErrorStatus);
    }

    [Fact]
    public void Parse_ResultWithDenials_ReadsDeniedTool()
    {
        var result = First<ResultMessage>("permission_deny", _ => true);

        var denial = Assert.Single(result.PermissionDenials);
        Assert.Equal("Write", denial.ToolName);
        Assert.Equal("你好", denial.ToolInput.GetProperty("content").GetString());
    }

    [Fact]
    public void Parse_StreamEvents_TextDeltasAddUpToFinalText()
    {
        var fixture = ProtocolFixture.Load("partial_messages");
        var messages = fixture.Received.Where(m => !m.GetProperty("type").GetString()!.StartsWith("control_", StringComparison.Ordinal)).Select(MessageParser.Parse).ToList();

        var streamed = string.Concat(messages.OfType<StreamEvent>().Select(e => e.TextDelta));
        var final = messages.OfType<AssistantMessage>().Single().Content.OfType<TextBlock>().Single().Text;

        Assert.Equal(final, streamed);
        Assert.Contains(messages.OfType<StreamEvent>(), e => e.EventType == "message_start" && e.TextDelta is null);
    }

    [Fact]
    public void Parse_RateLimitEvent_ReadsLimitInfo()
    {
        var limit = First<RateLimitEvent>("basic", _ => true);
        var raw = limit.Raw.GetProperty("rate_limit_info");

        Assert.Equal(raw.GetProperty("status").GetString(), limit.Info.Status);
        Assert.Equal(raw.GetProperty("rateLimitType").GetString(), limit.Info.RateLimitType);
        Assert.Equal(raw.GetProperty("resetsAt").GetInt64(), limit.Info.ResetsAt);
        Assert.Equal(raw.GetProperty("overageDisabledReason").GetString(), limit.Info.OverageDisabledReason);
        Assert.True(JsonElement.DeepEquals(raw, limit.Info.Raw));
    }

    [Fact]
    public void Parse_ConversationReset_ReadsNewConversation()
    {
        var message = Parse("""{"type":"conversation_reset","new_conversation_id":"c2","uuid":"u1","session_id":"s1"}""");

        Assert.Equal("c2", Assert.IsType<ConversationResetMessage>(message).NewConversationId);
    }

    [Fact]
    public void Parse_UnknownType_KeepsRawInsteadOfDropping()
    {
        var message = Parse("""{"type":"prompt_suggestion","suggestion":"試試看 /review","session_id":"s1"}""");

        var unknown = Assert.IsType<UnknownMessage>(message);
        Assert.Equal("prompt_suggestion", unknown.Type);
        Assert.Equal("s1", unknown.SessionId);
        Assert.Equal("試試看 /review", unknown.Raw.GetProperty("suggestion").GetString());
    }

    [Fact]
    public void Parse_UnknownContentBlock_KeepsRaw()
    {
        var message = Parse("""{"type":"user","message":{"content":[{"type":"image","source":{"type":"base64"}}]},"parent_tool_use_id":null}""");

        var block = Assert.IsType<UnknownContentBlock>(Assert.Single(Assert.IsType<UserMessage>(message).Content));
        Assert.Equal("image", block.Type);
        Assert.Equal("base64", block.Raw.GetProperty("source").GetProperty("type").GetString());
    }

    [Theory]
    [InlineData("""{"type":"assistant","message":{"content":[]}}""", "model")]
    [InlineData("""{"type":"assistant","message":{"model":"m","content":"文字"}}""", "陣列")]
    [InlineData("""{"type":"assistant","message":{"model":"m","content":[{"type":"tool_use","id":"t","input":{}}]}}""", "name")]
    [InlineData("""{"type":"result","subtype":"success","is_error":false,"duration_ms":1,"num_turns":1,"session_id":"s"}""", "duration_api_ms")]
    [InlineData("""{"type":"system"}""", "subtype")]
    [InlineData("""{"type":"stream_event","event":{},"session_id":"s"}""", "uuid")]
    public void Parse_KnownTypeMissingRequiredField_Throws(string json, string mentioned)
    {
        var error = Assert.Throws<MessageParseException>(() => Parse(json));

        Assert.Contains(mentioned, error.Message, StringComparison.Ordinal);
        Assert.NotNull(error.Raw);
    }

    [Theory]
    [InlineData("""{"session_id":"s1"}""")]
    [InlineData("""["not","an","object"]""")]
    public void Parse_NoTypeOrNotObject_Throws(string json)
    {
        Assert.Throws<MessageParseException>(() => Parse(json));
    }

    [Fact]
    public void ParseContextUsage_PythonSdkShape_ReadsListsAndLeavesNewerFieldsEmpty()
    {
        // Python SDK 測試用的形狀：比 CLI 2.1.309 舊，category 沒有 id、kind，也沒有 autoCompactThreshold；
        // 錄製時不載入使用者設定，樣本裡的 memoryFiles、mcpTools、agents 都是空的，所以在這裡測
        var usage = MessageParser.ParseContextUsage(JsonSerializer.Deserialize<JsonElement>("""
            {
              "categories": [
                {"name": "System prompt", "tokens": 3200, "color": "#888"},
                {"name": "MCP tools (deferred)", "tokens": 164, "color": "#999", "isDeferred": true}
              ],
              "totalTokens": 98200, "maxTokens": 155000, "rawMaxTokens": 200000, "percentage": 49.1,
              "model": "claude-sonnet-4-5", "isAutoCompactEnabled": true,
              "memoryFiles": [{"path": "/home/u/project/CLAUDE.md", "type": "Project", "tokens": 512}],
              "mcpTools": [{"name": "mcp__ref__search", "serverName": "ref", "tokens": 164}],
              "agents": [{"agentType": "coder", "source": "sdk", "tokens": 299}],
              "gridRows": [], "apiUsage": null
            }
            """));

        Assert.Equal(49.1, usage.Percentage);
        Assert.Null(usage.AutoCompactThreshold);
        Assert.All(usage.Categories, category => Assert.Null(category.Id));
        Assert.All(usage.Categories, category => Assert.Null(category.Kind));
        Assert.Equal([false, true], usage.Categories.Select(category => category.IsDeferred));
        Assert.Equal("#888", usage.Categories[0].Raw.GetProperty("color").GetString());
        Assert.Equal(new ContextUsageMemoryFile { Path = "/home/u/project/CLAUDE.md", Type = "Project", Tokens = 512 }, usage.MemoryFiles.Single() with { Raw = default });
        var tool = usage.McpTools.Single();
        Assert.Equal(("mcp__ref__search", "ref", 164), (tool.Name, tool.ServerName, tool.Tokens));
        Assert.Null(tool.IsLoaded);
        Assert.Equal(new ContextUsageAgent { AgentType = "coder", Source = "sdk", Tokens = 299 }, usage.Agents.Single() with { Raw = default });
    }

    [Fact]
    public void ParseContextUsage_ListsMissing_AreEmpty()
    {
        var usage = MessageParser.ParseContextUsage(JsonSerializer.Deserialize<JsonElement>("""
            {"categories": [], "totalTokens": 0, "maxTokens": 1, "rawMaxTokens": 1, "percentage": 0, "model": "m", "isAutoCompactEnabled": false}
            """));

        Assert.Empty(usage.MemoryFiles);
        Assert.Empty(usage.McpTools);
        Assert.Empty(usage.Agents);
    }

    [Theory]
    [InlineData("""{"categories": [], "totalTokens": 0, "maxTokens": 1, "rawMaxTokens": 1, "percentage": 0, "isAutoCompactEnabled": false}""", "model")]
    [InlineData("""{"totalTokens": 0, "maxTokens": 1, "rawMaxTokens": 1, "percentage": 0, "model": "m", "isAutoCompactEnabled": false}""", "categories")]
    [InlineData("""{"categories": [{"tokens": 1}], "totalTokens": 0, "maxTokens": 1, "rawMaxTokens": 1, "percentage": 0, "model": "m", "isAutoCompactEnabled": false}""", "name")]
    [InlineData("""{"categories": [], "totalTokens": 0, "maxTokens": 1, "rawMaxTokens": 1, "percentage": 0, "model": "m", "isAutoCompactEnabled": "yes"}""", "get_context_usage")]
    public void ParseContextUsage_MissingOrMalformedField_Throws(string json, string mentioned)
    {
        var error = Assert.Throws<MessageParseException>(() => MessageParser.ParseContextUsage(JsonSerializer.Deserialize<JsonElement>(json)));

        Assert.Contains(mentioned, error.Message, StringComparison.Ordinal);
        Assert.NotNull(error.Raw);
    }

    private static ClaudeMessage Parse(string json) => MessageParser.Parse(JsonSerializer.Deserialize<JsonElement>(json));

    private static T First<T>(string fixture, Func<T, bool> predicate)
        where T : ClaudeMessage =>
        ProtocolFixture.Load(fixture).Received
            .Where(m => !m.GetProperty("type").GetString()!.StartsWith("control_", StringComparison.Ordinal))
            .Select(MessageParser.Parse)
            .OfType<T>()
            .First(predicate);
}
