using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PamisWang.ClaudeAgentSdk.Transport;

/// <summary>
/// 連線前執行 <c>claude -v</c>，版本太舊就警告，不擋連線。對應 Python SDK <c>SubprocessCLITransport</c> 的 <c>_check_claude_version</c>。
/// </summary>
internal static partial class CliVersionCheck
{
    /// <summary>設成任何非空的值就不檢查。跟 Python SDK 用同一個名稱。</summary>
    public const string SkipVariable = "CLAUDE_AGENT_SDK_SKIP_VERSION_CHECK";

    /// <summary>支援的最低版本，跟 Python SDK 的 <c>MINIMUM_CLAUDE_CODE_VERSION</c> 一樣。</summary>
    public static Version MinimumVersion { get; } = new(2, 0, 0);

    /// <summary>
    /// 版本低於 <see cref="MinimumVersion"/> 就呼叫 <paramref name="warn"/>。
    /// 啟動失敗、逾時、看不懂輸出都當作沒事：這只是提醒，真正的錯誤留給之後啟動 CLI 時回報。
    /// </summary>
    public static async Task RunAsync(
        string cliPath,
        ClaudeAgentOptions options,
        Action<string> warn,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (await QueryAsync(cliPath, options, timeout, cancellationToken).ConfigureAwait(false) is { } version
            && version < MinimumVersion)
        {
            warn($"Claude Code {version}（{cliPath}）低於這個套件支援的最低版本 {MinimumVersion}，部分功能可能無法正常運作。"
                + "請執行 claude update 更新。");
        }
    }

    /// <summary>從 <c>claude -v</c> 的輸出（例如 <c>2.1.309 (Claude Code)</c>）取出開頭的版本號；看不懂就是 <see langword="null"/>。</summary>
    public static Version? Parse(string output) =>
        VersionPattern().Match(output.Trim()) is { Success: true } match && Version.TryParse(match.Value, out var version)
            ? version
            : null;

    private static async Task<Version?> QueryAsync(
        string cliPath,
        ClaudeAgentOptions options,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(cliPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            // 不讓 CLI 的錯誤訊息跑到呼叫端的主控台
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-v");
        // 環境變數跟正式啟動時一樣，呼叫端在 EnvironmentVariables 改的設定也會套用
        CliEnvironment.Apply(startInfo.Environment, options);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        Process? process = null;
        try
        {
            process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            // 只讀第一行，不等 stdout 關閉：CLI 開的子程序可能還握著它
            var line = await process.StandardOutput.ReadLineAsync(timeoutSource.Token).ConfigureAwait(false);
            return line is null ? null : Parse(line);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // 逾時
            return null;
        }
        catch (Exception e) when (e is Win32Exception or IOException or InvalidOperationException)
        {
            // 啟動不了或讀不到
            return null;
        }
        finally
        {
            if (process is not null)
            {
                KillIfRunning(process);
                process.Dispose();
            }
        }
    }

    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception)
        {
            // 剛好在這之間自己結束了
        }
    }

    [GeneratedRegex("^[0-9]+\\.[0-9]+\\.[0-9]+")]
    private static partial Regex VersionPattern();
}
