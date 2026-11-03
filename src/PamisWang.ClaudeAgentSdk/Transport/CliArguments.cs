using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text.Json.Nodes;
using PamisWang.ClaudeAgentSdk.Mcp;
using PamisWang.ClaudeAgentSdk.Protocol;

namespace PamisWang.ClaudeAgentSdk.Transport;

/// <summary>
/// 把選項轉成 CLI 參數。對應 Python SDK 的 <c>_build_command</c>，參數順序也一樣，方便對照協定樣本裡的 argv；
/// Python 沒有、TypeScript SDK 才有的旗標，放在 TypeScript SDK 排的位置附近。
/// </summary>
internal static class CliArguments
{
    // cmd.exe 會解讀的字元
    private const string CmdExeMetacharacters = "&|<>^%!\"\r\n";
    private static readonly SearchValues<char> CmdExeMetacharacterValues = SearchValues.Create(CmdExeMetacharacters);

    /// <summary>組出參數，不含執行檔本身。</summary>
    /// <exception cref="ArgumentException">
    /// 選項互相衝突（見 <see cref="ClaudeAgentClient.ConnectAsync"/>），
    /// Windows 上會接到命令列的 session 相關值含有 cmd.exe 的特殊字元，
    /// 或 <see cref="ClaudeAgentOptions.ExtraArguments"/> 的旗標名稱不能用。
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="ClaudeAgentOptions.MaxTurns"/>、<see cref="ClaudeAgentOptions.MaxBudgetUsd"/> 或 <see cref="ClaudeAgentOptions.TaskBudget"/> 不大於 0。
    /// </exception>
    public static IReadOnlyList<string> Build(ClaudeAgentOptions options, bool isWindows)
    {
        RejectConflicts(options);
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

        if (options.Tools is { } tools)
        {
            // 空的清單也要送：空字串代表停用所有內建工具，跟 Python 一樣
            args.AddRange(["--tools", string.Join(',', tools)]);
        }

        var allowedTools = EffectiveAllowedTools(options);
        if (allowedTools.Count > 0)
        {
            args.AddRange(["--allowedTools", string.Join(',', allowedTools)]);
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

        if (options.TaskBudget is { } taskBudget)
        {
            args.AddRange(["--task-budget", FormatPositive(nameof(options.TaskBudget), taskBudget)]);
        }

        if (!string.IsNullOrEmpty(options.Model))
        {
            args.AddRange(["--model", options.Model]);
        }

        // TS SDK 排在 --model 後面
        if (!string.IsNullOrEmpty(options.Agent))
        {
            AddOption(args, "--agent", options.Agent);
        }

        if (options.CanUseTool is not null)
        {
            // 權限詢問改走 stdin／stdout 的 control 協定，由 CanUseTool 回覆
            args.AddRange(["--permission-prompt-tool", "stdio"]);
        }
        else if (!string.IsNullOrEmpty(options.PermissionPromptToolName))
        {
            AddOption(args, "--permission-prompt-tool", options.PermissionPromptToolName);
        }

        if (options.DisablePermissionPrompts)
        {
            args.AddRange(["--permission-prompts", "none"]);
        }

        if (options.PermissionMode is { } permissionMode)
        {
            AddOption(args, "--permission-mode", permissionMode.Value);
        }

        if (options.AllowDangerouslySkipPermissions)
        {
            args.Add("--allow-dangerously-skip-permissions");
        }

        if (options.ContinueConversation)
        {
            args.Add("--continue");
        }

        // 這幾個寫成 --旗標=<值>：--resume 的值可以省略，分成兩個參數時，- 開頭的值會被當成另一個旗標；其他的跟 Python 一樣也用 = 綁住
        AddSessionValue(args, "--resume", nameof(options.ResumeSessionId), options.ResumeSessionId, isWindows);
        AddSessionValue(args, "--session-id", nameof(options.SessionId), options.SessionId, isWindows);

        if (BuildSettings(options) is { } settings)
        {
            AddOption(args, "--settings", settings);
        }

        if (options.ManagedSettings is { } managedSettings)
        {
            args.AddRange(["--managed-settings", managedSettings.ToJsonString()]);
        }

        if (options.McpServers.Count > 0)
        {
            // JSON 物件一定是 { 開頭，不會被 CLI 當成另一個旗標
            args.AddRange(["--mcp-config", McpConfigJson(options.McpServers)]);
        }

        if (options.IncludePartialMessages)
        {
            args.Add("--include-partial-messages");
        }

        if (options.StrictMcpConfig)
        {
            args.Add("--strict-mcp-config");
        }

        if (options.ForkSession)
        {
            args.Add("--fork-session");
        }

        AddSessionValue(args, "--resume-session-at", nameof(options.ResumeSessionAt), options.ResumeSessionAt, isWindows);

        // 跟 Python 一樣看是不是 null 而不是空字串：空字串也送，讓 CLI 以格式錯誤拒絕，而不是默默關掉呼叫端以為開著的檢查
        if (options.ResumeDropsTurn is { } resumeDropsTurn)
        {
            if (isWindows)
            {
                RejectWindowsCmdMetacharacters(nameof(options.ResumeDropsTurn), resumeDropsTurn);
            }

            args.Add($"--resume-drops-turn={resumeDropsTurn}");
        }

        if (!options.PersistSession)
        {
            args.Add("--no-session-persistence");
        }

        if (options.SettingSources is { } settingSources)
        {
            // 空的清單寫成 --setting-sources=，代表不載入任何設定檔
            args.Add($"--setting-sources={string.Join(',', settingSources.Select(source => source.Value))}");
        }

        if (options.PluginDelivery == PluginDelivery.Initialize && options.Plugins.Count > 0)
        {
            // 外掛改放在 initialize 裡送，CLI 要先等 initialize 才載入
            args.Add("--await-initialize");
        }
        else
        {
            foreach (var plugin in options.Plugins)
            {
                AddOption(args, plugin.SkipMcpDiscovery ? "--plugin-dir-no-mcp" : "--plugin-dir", plugin.Path);
            }
        }

        // 跟 Python 一樣排在 --effort 前面。值不檢查 cmd.exe 的特殊字元：JSON 值一定有引號，
        // 而且參數不經過 shell，Windows 上也不執行批次檔；Python 同樣不檢查
        var permissionPromptToolIsSet = options.CanUseTool is not null || !string.IsNullOrEmpty(options.PermissionPromptToolName);
        foreach (var (flag, value) in options.ExtraArguments)
        {
            RejectInvalidExtraFlag(nameof(options.ExtraArguments), flag, permissionPromptToolIsSet);
            if (value is null)
            {
                args.Add($"--{flag}");
            }
            else
            {
                AddOption(args, $"--{flag}", value);
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

    /// <summary>實際送出的 <c>--allowedTools</c>：呼叫端給的，加上 <see cref="ClaudeAgentOptions.Skills"/> 需要的 <c>Skill</c> 規則。</summary>
    internal static IReadOnlyList<string> EffectiveAllowedTools(ClaudeAgentOptions options)
    {
        if (options.Skills is not { } skills)
        {
            return options.AllowedTools;
        }

        List<string> allowed = [.. options.AllowedTools];
        IEnumerable<string> rules = skills.Names is { } names ? names.Select(name => $"Skill({name})") : ["Skill"];
        foreach (var rule in rules)
        {
            if (!allowed.Contains(rule))
            {
                allowed.Add(rule);
            }
        }

        return allowed;
    }

    /// <summary>
    /// 一個參數帶一個值：值以 <c>-</c> 開頭時寫成 <c>--旗標=值</c>，免得值可以省略的旗標把它當成另一個旗標；
    /// 其他情況分成兩個參數，跟 Python 一樣。
    /// </summary>
    private static void AddOption(List<string> args, string flag, string value)
    {
        if (value.StartsWith('-'))
        {
            args.Add($"{flag}={value}");
        }
        else
        {
            args.AddRange([flag, value]);
        }
    }

    /// <summary>session 相關的值：常來自外部輸入，Windows 上多擋一層 cmd.exe 的特殊字元，一律寫成 <c>--旗標=值</c>。</summary>
    private static void AddSessionValue(List<string> args, string flag, string optionName, string? value, bool isWindows)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        if (isWindows)
        {
            RejectWindowsCmdMetacharacters(optionName, value);
        }

        args.Add($"{flag}={value}");
    }

    /// <summary>
    /// <c>--settings</c> 的值：<see cref="ClaudeAgentOptions.SettingsFile"/> 的路徑，或是 <see cref="ClaudeAgentOptions.Settings"/>
    /// 併上 <see cref="ClaudeAgentOptions.Sandbox"/> 的 JSON。都沒有時是 <see langword="null"/>。
    /// </summary>
    private static string? BuildSettings(ClaudeAgentOptions options)
    {
        if (options.SettingsFile is { } file)
        {
            return file;
        }

        if (options.Sandbox is null)
        {
            return options.Settings?.ToJsonString();
        }

        // 不改呼叫端的物件：複製一份再放進 sandbox
        var settings = options.Settings?.DeepClone().AsObject() ?? new JsonObject();
        settings["sandbox"] = options.Sandbox.DeepClone();
        return settings.ToJsonString();
    }

    private static string McpConfigJson(IReadOnlyDictionary<string, McpServerConfig> servers) =>
        ControlMessages.Json(writer =>
        {
            writer.WriteStartObject("mcpServers");
            foreach (var (name, server) in servers)
            {
                writer.WritePropertyName(name);
                server.WriteTo(writer);
            }

            writer.WriteEndObject();
        });

    /// <summary>
    /// 擋下互相衝突、CLI 會拒絕或默默忽略的選項組合。Python 多半不檢查；
    /// 這些限制來自 CLI 與 TypeScript SDK 的說明（例如 <c>--fork-session</c> 要搭配 <c>--resume</c>）。
    /// </summary>
    private static void RejectConflicts(ClaudeAgentOptions options)
    {
        var resumes = !string.IsNullOrEmpty(options.ResumeSessionId);
        var problem = options switch
        {
            { ContinueConversation: true } when resumes =>
                (nameof(options.ContinueConversation), "不能跟 ResumeSessionId 同時指定：一個接續最近的對話，一個接續指定的 session"),
            { ForkSession: true } when !resumes && !options.ContinueConversation =>
                (nameof(options.ForkSession), "要搭配 ResumeSessionId 或 ContinueConversation，分岔的是要接續的那個 session"),
            { SessionId.Length: > 0 } when (resumes || options.ContinueConversation) && !options.ForkSession =>
                (nameof(options.SessionId), "搭配 ResumeSessionId 或 ContinueConversation 時，要同時設定 ForkSession，用來指定分岔出來的新 session 的 ID"),
            { ResumeSessionAt.Length: > 0 } when !resumes =>
                (nameof(options.ResumeSessionAt), "要搭配 ResumeSessionId"),
            { ResumeDropsTurn: not null } when string.IsNullOrEmpty(options.ResumeSessionAt) =>
                (nameof(options.ResumeDropsTurn), "要搭配 ResumeSessionAt"),
            { CanUseTool: not null, PermissionPromptToolName.Length: > 0 } =>
                (nameof(options.PermissionPromptToolName), "不能跟 CanUseTool 同時設定，權限詢問只能交給其中一個"),
            { DisablePermissionPrompts: true } when options.CanUseTool is not null || !string.IsNullOrEmpty(options.PermissionPromptToolName) =>
                (nameof(options.DisablePermissionPrompts), "不能跟 CanUseTool 或 PermissionPromptToolName 同時設定：沒有人回答時它們不會被呼叫"),
            { Settings: not null, SettingsFile: not null } =>
                (nameof(options.SettingsFile), "不能跟 Settings 同時設定"),
            { SettingsFile: not null, Sandbox: not null } =>
                (nameof(options.Sandbox), "不能跟 SettingsFile 同時設定：檔案的內容這裡不會讀，請把沙箱設定寫進那個檔案"),
            { Settings: { } settings, Sandbox: not null } when settings.ContainsKey("sandbox") =>
                (nameof(options.Sandbox), "Settings 裡已經有 sandbox，兩邊只能留一個"),
            _ => default((string Option, string Reason)?),
        };

        if (problem is var (option, reason))
        {
            throw new ArgumentException($"{option} {reason}。", option);
        }
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
    private static void RejectInvalidExtraFlag(string optionName, string flag, bool permissionPromptToolIsSet)
    {
        var reason = flag switch
        {
            _ when string.IsNullOrWhiteSpace(flag) => "名稱是空的",
            _ when flag.StartsWith('-') => "名稱不用加開頭的 --",
            _ when flag.Contains('=') => "名稱不能含 =，值請放在 value",
            "output-format" or "input-format" => "這個套件固定用 stream-json 跟 CLI 溝通，不能改",
            "permission-prompt-tool" when permissionPromptToolIsSet => "設定了 CanUseTool 或 PermissionPromptToolName 時，由它們決定權限詢問交給誰",
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
