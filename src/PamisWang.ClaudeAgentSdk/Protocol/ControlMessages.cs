using System.Buffers;
using System.Text;
using System.Text.Json;

namespace PamisWang.ClaudeAgentSdk.Protocol;

/// <summary>
/// SDK 寫進 CLI stdin 的訊息，每個方法回傳一整行（含結尾換行）。欄位與順序跟 Python SDK 送出的一樣。
/// </summary>
internal static class ControlMessages
{
    /// <summary>SDK 發給 CLI 的 control request。</summary>
    public static string Request(string requestId, string subtype, Action<Utf8JsonWriter>? writeFields = null) =>
        Line(writer =>
        {
            writer.WriteString("type", "control_request");
            writer.WriteString("request_id", requestId);
            writer.WriteStartObject("request");
            writer.WriteString("subtype", subtype);
            writeFields?.Invoke(writer);
            writer.WriteEndObject();
        });

    /// <summary>回覆 CLI 的 control request：成功。</summary>
    public static string Success(string requestId, Action<Utf8JsonWriter> writeResponseFields) =>
        Response(requestId, "success", writer =>
        {
            writer.WriteStartObject("response");
            writeResponseFields(writer);
            writer.WriteEndObject();
        });

    /// <summary>回覆 CLI 的 control request：處理失敗。</summary>
    public static string Error(string requestId, string error) =>
        Response(requestId, "error", writer => writer.WriteString("error", error));

    /// <summary>
    /// 使用者說的一句話。<paramref name="clientComposed"/> 是 <see langword="true"/> 時加上 <c>client_composed</c>，
    /// 讓 CLI 照原文送出（<see cref="ClaudeAgentOptions.VerbatimPrompts"/>）；跟 Python 一樣放在最後。
    /// </summary>
    public static string UserMessage(string content, string sessionId, bool clientComposed = false) =>
        Line(writer =>
        {
            writer.WriteString("type", "user");
            writer.WriteStartObject("message");
            writer.WriteString("role", "user");
            writer.WriteString("content", content);
            writer.WriteEndObject();
            writer.WriteNull("parent_tool_use_id");
            writer.WriteString("session_id", sessionId);
            if (clientComposed)
            {
                writer.WriteBoolean("client_composed", true);
            }
        });

    /// <summary>
    /// initialize 的欄位。<c>hooks</c> 一律是 <see langword="null"/>（不支援 hook 回呼）；其他的只在選項有設定時才寫，
    /// 沒設定時送出的跟 Python SDK 一樣只有 <c>hooks</c>。順序：Python 也有的照 Python，TypeScript SDK 才有的放在後面。
    /// </summary>
    public static void WriteInitializeFields(Utf8JsonWriter writer, ClaudeAgentOptions options)
    {
        writer.WriteNull("hooks");
        if (options.Agents.Count > 0)
        {
            writer.WriteStartObject("agents");
            foreach (var (name, agent) in options.Agents)
            {
                writer.WritePropertyName(name);
                agent.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        // 全部的 skill 不用送：沒有篩選跟不送一樣
        if (options.Skills?.Names is { } skills)
        {
            writer.WriteStartArray("skills");
            foreach (var skill in skills)
            {
                writer.WriteStringValue(skill);
            }

            writer.WriteEndArray();
        }

        if (options.ForwardSubagentText)
        {
            writer.WriteBoolean("forwardSubagentText", true);
        }

        if (options.PlanModeInstructions is not null)
        {
            writer.WriteString("planModeInstructions", options.PlanModeInstructions);
        }

        if (options.ToolAliases.Count > 0)
        {
            writer.WriteStartObject("toolAliases");
            foreach (var (alias, target) in options.ToolAliases)
            {
                writer.WriteString(alias, target);
            }

            writer.WriteEndObject();
        }

        if (options.AgentProgressSummaries)
        {
            writer.WriteBoolean("agentProgressSummaries", true);
        }

        if (options.PerTaskStopAffordance)
        {
            writer.WriteBoolean("perTaskStopAffordance", true);
        }

        if (options.PluginDelivery == PluginDelivery.Initialize && options.Plugins.Count > 0)
        {
            writer.WriteStartArray("plugins");
            foreach (var plugin in options.Plugins)
            {
                WritePlugin(writer, plugin);
            }

            writer.WriteEndArray();
        }
    }

    /// <summary>一個本機外掛，格式跟 TypeScript SDK 的 <c>SdkPluginConfig</c> 一樣。</summary>
    public static void WritePlugin(Utf8JsonWriter writer, LocalPlugin plugin)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "local");
        writer.WriteString("path", plugin.Path);
        if (plugin.SkipMcpDiscovery)
        {
            writer.WriteBoolean("skipMcpDiscovery", true);
        }

        writer.WriteEndObject();
    }

    /// <summary>寫出一個 JSON 物件，不含換行；給 CLI 參數裡的 JSON 值用。</summary>
    public static string Json(Action<Utf8JsonWriter> writeFields)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writeFields(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static string Response(string requestId, string subtype, Action<Utf8JsonWriter> writeFields) =>
        Line(writer =>
        {
            writer.WriteString("type", "control_response");
            writer.WriteStartObject("response");
            writer.WriteString("subtype", subtype);
            writer.WriteString("request_id", requestId);
            writeFields(writer);
            writer.WriteEndObject();
        });

    private static string Line(Action<Utf8JsonWriter> writeFields) => Json(writeFields) + "\n";
}
