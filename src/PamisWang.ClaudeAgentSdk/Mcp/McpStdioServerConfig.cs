using System.Collections.ObjectModel;
using System.Text.Json;

namespace PamisWang.ClaudeAgentSdk.Mcp;

/// <summary>由 Claude Code 啟動子程序、經 stdin／stdout 溝通的 MCP server。對應 Python SDK 的 <c>McpStdioServerConfig</c>。</summary>
/// <param name="Command">要執行的程式。</param>
public sealed record McpStdioServerConfig(string Command) : McpServerConfig
{
    /// <summary>程式的參數。</summary>
    public IReadOnlyList<string> Args { get; init; } = [];

    /// <summary>子程序額外的環境變數。</summary>
    public IReadOnlyDictionary<string, string> Env { get; init; } = ReadOnlyDictionary<string, string>.Empty;

    /// <inheritdoc />
    private protected override void WriteFields(Utf8JsonWriter writer)
    {
        writer.WriteString("type", "stdio");
        writer.WriteString("command", Command);
        if (Args.Count > 0)
        {
            writer.WriteStartArray("args");
            foreach (var arg in Args)
            {
                writer.WriteStringValue(arg);
            }

            writer.WriteEndArray();
        }

        WriteStringMap(writer, "env", Env);
    }
}
