using System.Collections.ObjectModel;
using System.Text.Json;
using PamisWang.ClaudeAgentSdk.Mcp;
using PamisWang.ClaudeAgentSdk.Permissions;

namespace PamisWang.ClaudeAgentSdk;

/// <summary>
/// 自訂的 subagent，主對話可以用 Agent 工具交辦工作給它。用在 <see cref="ClaudeAgentOptions.Agents"/>，對應 Python SDK 的 <c>AgentDefinition</c>。
/// 沒指定的欄位不送，照 Claude Code 的預設（多半是沿用主對話的）。
/// </summary>
/// <param name="Description">什麼時候該用這個 agent；主對話靠這段說明決定要不要交辦。</param>
/// <param name="Prompt">這個 agent 的系統提示詞。</param>
public sealed record AgentDefinition(string Description, string Prompt)
{
    /// <summary>可以用的工具；<see langword="null"/> 是沿用主對話的全部工具。</summary>
    public IReadOnlyList<string>? Tools { get; init; }

    /// <summary>停用的工具。</summary>
    public IReadOnlyList<string>? DisallowedTools { get; init; }

    /// <summary>模型：<c>sonnet</c>、<c>opus</c>、<c>haiku</c>、<c>inherit</c>（跟主對話一樣）或完整的模型 ID。</summary>
    public string? Model { get; init; }

    /// <summary>預先載入到這個 agent 的 skill。</summary>
    public IReadOnlyList<string>? Skills { get; init; }

    /// <summary>
    /// 自動載入 agent memory 檔的範圍：<c>user</c>（<c>~/.claude/agent-memory/&lt;agent&gt;/</c>）、
    /// <c>project</c>（<c>.claude/agent-memory/&lt;agent&gt;/</c>）或 <c>local</c>（<c>.claude/agent-memory-local/&lt;agent&gt;/</c>）。
    /// </summary>
    public string? Memory { get; init; }

    /// <summary>這個 agent 可以用的 MCP server，寫已經設定好的 server 名稱。要在這裡直接定義 server，用 <see cref="InlineMcpServers"/>。</summary>
    public IReadOnlyList<string> McpServers { get; init; } = [];

    /// <summary>只給這個 agent 用的 MCP server，key 是 server 名稱。跟 <see cref="McpServers"/> 一起送進 CLI 的 <c>mcpServers</c>。</summary>
    public IReadOnlyDictionary<string, McpServerConfig> InlineMcpServers { get; init; } = ReadOnlyDictionary<string, McpServerConfig>.Empty;

    /// <summary>這個 agent 當主對話（<see cref="ClaudeAgentOptions.Agent"/>）時，自動送出的第一句話；會處理斜線指令，放在使用者說的話前面。</summary>
    public string? InitialPrompt { get; init; }

    /// <summary>模型來回次數的上限。</summary>
    public int? MaxTurns { get; init; }

    /// <summary>交辦時在背景執行，不等它做完。</summary>
    public bool? Background { get; init; }

    /// <summary>思考深度：<c>low</c>、<c>medium</c>、<c>high</c>、<c>xhigh</c> 或 <c>max</c>。</summary>
    public string? Effort { get; init; }

    /// <summary>這個 agent 的權限模式。</summary>
    public PermissionMode? PermissionMode { get; init; }

    /// <summary>
    /// 當 subagent 時，context 到這個 token 數就壓縮自己的對話；只能比沿用的門檻更低。當主對話時沒有作用。
    /// Python SDK 沒有這個欄位，對應 TypeScript SDK 的 <c>autoCompactWindow</c>。
    /// </summary>
    public int? AutoCompactWindow { get; init; }

    /// <summary>
    /// 當 subagent 時不載入使用者、專案、本機的 CLAUDE.md（組織管理的照樣載入），給完全靠交辦內容工作的 agent 用。當主對話時沒有作用。
    /// Python SDK 沒有這個欄位，對應 TypeScript SDK 的 <c>omitClaudeMd</c>。
    /// </summary>
    public bool? OmitClaudeMd { get; init; }

    /// <summary>
    /// 這個 agent 執行時，自動在背景啟動的觀察者 agent 類型：只收到唯讀的活動摘要，用 ObserverReport 工具回報，不參與工作。
    /// Python SDK 沒有這個欄位，對應 TypeScript SDK 的 <c>observer</c>。
    /// </summary>
    public string? Observer { get; init; }

    /// <summary>附加在每份送給觀察者的活動摘要後面的說明。Python SDK 沒有這個欄位，對應 TypeScript SDK 的 <c>observerMessage</c>。</summary>
    public string? ObserverMessage { get; init; }

    /// <summary>寫成 initialize 的 <c>agents</c> 裡的一項。欄位順序跟 Python 的 dataclass 一樣，沒指定的不寫；TS SDK 才有的放在後面。</summary>
    internal void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("description", Description);
        writer.WriteString("prompt", Prompt);
        WriteStrings(writer, "tools", Tools);
        WriteStrings(writer, "disallowedTools", DisallowedTools);
        WriteOptional(writer, "model", Model);
        WriteStrings(writer, "skills", Skills);
        WriteOptional(writer, "memory", Memory);
        if (McpServers.Count > 0 || InlineMcpServers.Count > 0)
        {
            writer.WriteStartArray("mcpServers");
            foreach (var name in McpServers)
            {
                writer.WriteStringValue(name);
            }

            foreach (var (name, server) in InlineMcpServers)
            {
                writer.WriteStartObject();
                writer.WritePropertyName(name);
                server.WriteTo(writer);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        WriteOptional(writer, "initialPrompt", InitialPrompt);
        WriteOptional(writer, "maxTurns", MaxTurns);
        WriteOptional(writer, "background", Background);
        WriteOptional(writer, "effort", Effort);
        WriteOptional(writer, "permissionMode", PermissionMode?.Value);
        WriteOptional(writer, "autoCompactWindow", AutoCompactWindow);
        WriteOptional(writer, "omitClaudeMd", OmitClaudeMd);
        WriteOptional(writer, "observer", Observer);
        WriteOptional(writer, "observerMessage", ObserverMessage);
        writer.WriteEndObject();
    }

    private static void WriteStrings(Utf8JsonWriter writer, string name, IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return;
        }

        writer.WriteStartArray(name);
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static void WriteOptional(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }

    private static void WriteOptional(Utf8JsonWriter writer, string name, int? value)
    {
        if (value is { } number)
        {
            writer.WriteNumber(name, number);
        }
    }

    private static void WriteOptional(Utf8JsonWriter writer, string name, bool? value)
    {
        if (value is { } flag)
        {
            writer.WriteBoolean(name, flag);
        }
    }
}
