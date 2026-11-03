using System.Buffers;
using System.Globalization;
using System.Numerics;

namespace PamisWang.ClaudeAgentSdk.Transport;

/// <summary>
/// 把選項轉成 CLI 參數。對應 Python SDK 的 <c>_build_command</c>，參數順序也一樣，方便對照協定樣本裡的 argv。
/// </summary>
internal static class CliArguments
{
    // cmd.exe 會解讀的字元
    private const string CmdExeMetacharacters = "&|<>^%!\"\r\n";
    private static readonly SearchValues<char> CmdExeMetacharacterValues = SearchValues.Create(CmdExeMetacharacters);

    /// <summary>組出參數，不含執行檔本身。</summary>
    /// <exception cref="ArgumentException">
    /// Windows 上 <see cref="ClaudeAgentOptions.ResumeSessionId"/> 含有 cmd.exe 的特殊字元，
    /// 或 <see cref="ClaudeAgentOptions.ExtraArguments"/> 的旗標名稱不能用。
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="ClaudeAgentOptions.MaxTurns"/> 或 <see cref="ClaudeAgentOptions.MaxBudgetUsd"/> 不大於 0。
    /// </exception>
    public static IReadOnlyList<string> Build(ClaudeAgentOptions options, bool isWindows)
    {
        List<string> args = ["--output-format", "stream-json", "--verbose"];

        switch (options.SystemPrompt)
        {
            case null:
                // 沒指定系統提示時傳空字串，不用 Claude Code 預設的系統提示；Python 與 TS SDK 都是這樣
                args.AddRange(["--system-prompt", ""]);
                break;
            case SystemPromptCustom custom:
                args.AddRange(["--system-prompt", custom.Prompt]);
                break;
            case SystemPromptFile file:
                args.AddRange(["--system-prompt-file", file.Path]);
                break;
            case SystemPromptPreset { Append: { } append }:
                args.AddRange(["--append-system-prompt", append]);
                break;
            default:
                // 只用預設的系統提示：不帶參數，CLI 就用自己的
                break;
        }

        // 跟 Python 一樣排在 --disallowedTools 前面
        if (options.MaxTurns is { } maxTurns)
        {
            args.AddRange(["--max-turns", FormatPositive(nameof(options.MaxTurns), maxTurns)]);
        }

        if (options.MaxBudgetUsd is { } maxBudgetUsd)
        {
            args.AddRange(["--max-budget-usd", FormatPositive(nameof(options.MaxBudgetUsd), maxBudgetUsd)]);
        }

        if (options.DisallowedTools.Count > 0)
        {
            args.AddRange(["--disallowedTools", string.Join(',', options.DisallowedTools)]);
        }

        if (!string.IsNullOrEmpty(options.Model))
        {
            args.AddRange(["--model", options.Model]);
        }

        if (options.CanUseTool is not null)
        {
            // 權限詢問改走 stdin／stdout 的 control 協定，由 CanUseTool 回覆
            args.AddRange(["--permission-prompt-tool", "stdio"]);
        }

        if (!string.IsNullOrEmpty(options.ResumeSessionId))
        {
            if (isWindows)
            {
                RejectWindowsCmdMetacharacters(nameof(options.ResumeSessionId), options.ResumeSessionId);
            }

            // 寫成 --resume=<值>：--resume 的值可以省略，分成兩個參數時，- 開頭的值會被當成另一個旗標
            args.Add($"--resume={options.ResumeSessionId}");
        }

        if (options.IncludePartialMessages)
        {
            args.Add("--include-partial-messages");
        }

        // 跟 Python 一樣排在 --effort 前面。值不檢查 cmd.exe 的特殊字元：JSON 值一定有引號，
        // 而且參數不經過 shell，Windows 上也不執行批次檔；Python 同樣不檢查
        foreach (var (flag, value) in options.ExtraArguments)
        {
            RejectInvalidExtraFlag(nameof(options.ExtraArguments), flag, options.CanUseTool is not null);
            if (value is null)
            {
                args.Add($"--{flag}");
            }
            else if (value.StartsWith('-'))
            {
                // 值可以省略的旗標，分成兩個參數時 - 開頭的值會被當成另一個旗標；寫成 = 一定綁在一起
                args.Add($"--{flag}={value}");
            }
            else
            {
                args.AddRange([$"--{flag}", value]);
            }
        }

        if (options.Effort is not null)
        {
            args.AddRange(["--effort", options.Effort]);
        }

        if (options.OutputSchema is not null)
        {
            // 跟 Python 一樣排在 --effort 後面。JSON 物件一定是 { 開頭，不會被 CLI 當成另一個旗標
            args.AddRange(["--json-schema", options.OutputSchema.ToJsonString()]);
        }

        args.AddRange(["--input-format", "stream-json"]);
        return args;
    }

    /// <summary>
    /// 上限類的選項必須大於 0：0 沒有意義，負數會送出 <c>-</c> 開頭、被 CLI 當成另一個旗標的值。
    /// Python 遇到 0 略過旗標、負數照送。數字照 <see cref="CultureInfo.InvariantCulture"/> 寫，
    /// 不然在小數點是逗號的語系（例如 de-DE）會送出 <c>1,5</c>。
    /// </summary>
    private static string FormatPositive<T>(string optionName, T value)
        where T : INumber<T>
    {
        if (value <= T.Zero)
        {
            throw new ArgumentOutOfRangeException(optionName, value, $"{optionName} 必須大於 0。");
        }

        return value.ToString(null, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 擋下組不出正確參數的旗標名稱，以及這個套件自己要控制的旗標。Python 不檢查：
    /// 例如 key 寫成 <c>--max-turns</c> 會送出 <c>----max-turns</c>，蓋掉 <c>output-format</c> 會讀不到 stream-json。
    /// </summary>
    private static void RejectInvalidExtraFlag(string optionName, string flag, bool hasCanUseTool)
    {
        var reason = flag switch
        {
            _ when string.IsNullOrWhiteSpace(flag) => "名稱是空的",
            _ when flag.StartsWith('-') => "名稱不用加開頭的 --",
            _ when flag.Contains('=') => "名稱不能含 =，值請放在 value",
            "output-format" or "input-format" => "這個套件固定用 stream-json 跟 CLI 溝通，不能改",
            "permission-prompt-tool" when hasCanUseTool => "設定了 CanUseTool 時，權限詢問固定走 stdio",
            _ => null,
        };

        if (reason is not null)
        {
            throw new ArgumentException($"{optionName} 的旗標「{flag}」不能用：{reason}。", optionName);
        }
    }

    /// <summary>
    /// 多一層防護：已經拒絕執行批次檔，這些字元其實無害；但 resume 的值常來自外部輸入，
    /// 萬一之後又多了一層 cmd.exe，也不會變成指令。
    /// </summary>
    private static void RejectWindowsCmdMetacharacters(string optionName, string value)
    {
        if (value.AsSpan().ContainsAny(CmdExeMetacharacterValues))
        {
            throw new ArgumentException(
                $"{optionName} 的值 {value} 含有不能放在 Windows 命令列上的字元（{CmdExeMetacharacters.ReplaceLineEndings(" ")}）。",
                optionName);
        }
    }
}
