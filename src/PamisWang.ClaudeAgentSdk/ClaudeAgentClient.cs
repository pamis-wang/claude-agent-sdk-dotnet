using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using PamisWang.ClaudeAgentSdk.Mcp;
using PamisWang.ClaudeAgentSdk.Messages;
using PamisWang.ClaudeAgentSdk.Permissions;
using PamisWang.ClaudeAgentSdk.Protocol;
using PamisWang.ClaudeAgentSdk.Transport;

namespace PamisWang.ClaudeAgentSdk;

/// <summary>
/// 跟 Claude Code 互動式對話：啟動本機的 CLI、送出訊息、接收回覆、批准工具、中斷。對應 Python SDK 的 <c>ClaudeSDKClient</c>。
/// 一個 client 只連線一次；要重新開始就建立新的 client，用 <see cref="ClaudeAgentOptions.ResumeSessionId"/> 接續原本的 session。
/// </summary>
/// <example>
/// <code>
/// await using var client = new ClaudeAgentClient(new ClaudeAgentOptions { WorkingDirectory = @"D:\work" });
/// await client.ConnectAsync();
/// await client.SendAsync("這個專案在做什麼？");
/// await foreach (var message in client.ReceiveResponseAsync())
/// {
///     if (message is AssistantMessage assistant)
///     {
///         foreach (var text in assistant.Content.OfType&lt;TextBlock&gt;())
///         {
///             Console.WriteLine(text.Text);
///         }
///     }
/// }
/// </code>
/// </example>
public sealed class ClaudeAgentClient : IAsyncDisposable
{
    // 跟 Python SDK 一樣：initialize 至少等 60 秒（MCP server 啟動可能很慢），
    // 環境變數 CLAUDE_CODE_STREAM_CLOSE_TIMEOUT（毫秒）設得更長就用它
    private static readonly TimeSpan MinimumInitializeTimeout = TimeSpan.FromSeconds(60);

    private readonly ClaudeAgentOptions _options;
    private readonly ITransport _transport;
    private readonly Lock _gate = new();
    private ClientState _state;
    private ControlProtocol? _protocol;

    /// <summary>建立 client；要呼叫 <see cref="ConnectAsync"/> 才會啟動 Claude Code。</summary>
    public ClaudeAgentClient(ClaudeAgentOptions? options = null)
        : this(options ?? new ClaudeAgentOptions(), transport: null)
    {
    }

    internal ClaudeAgentClient(ClaudeAgentOptions options, ITransport? transport)
    {
        _options = options;
        _transport = transport ?? new SubprocessTransport(options);
    }

    private enum ClientState
    {
        New,
        Connecting,
        Connected,
        Closed,
    }

    /// <summary>
    /// 初始化握手時 CLI 回報的資訊：可用的指令、模型、輸出風格、帳號等。對應 Python 的 <c>get_server_info()</c>。還沒連線時是 <see langword="null"/>。
    /// </summary>
    public JsonElement? ServerInfo { get; private set; }

    /// <summary>已經連線，而且還沒關閉。</summary>
    public bool IsConnected
    {
        get
        {
            lock (_gate)
            {
                return _state == ClientState.Connected;
            }
        }
    }

    /// <summary>啟動 Claude Code 並完成初始化握手。失敗時會把已經啟動的 CLI 關掉，這個 client 也就不能再用。</summary>
    /// <exception cref="CliNotFoundException">找不到 Claude Code。</exception>
    /// <exception cref="CliConnectionException">啟動失敗，或 CLI 在握手完成前就結束。</exception>
    /// <exception cref="TimeoutException">CLI 遲遲沒有回應初始化。</exception>
    /// <exception cref="InvalidOperationException">已經連線過。</exception>
    /// <exception cref="ArgumentException">
    /// 選項組不出 CLI 參數：Windows 上的 <see cref="ClaudeAgentOptions.ResumeSessionId"/>、<see cref="ClaudeAgentOptions.SessionId"/>、
    /// <see cref="ClaudeAgentOptions.ResumeSessionAt"/> 或 <see cref="ClaudeAgentOptions.ResumeDropsTurn"/> 含有 cmd.exe 的特殊字元，
    /// <see cref="ClaudeAgentOptions.ExtraArguments"/> 的旗標名稱不能用，
    /// <see cref="ClaudeAgentOptions.MaxTurns"/>、<see cref="ClaudeAgentOptions.MaxBudgetUsd"/>、<see cref="ClaudeAgentOptions.TaskBudget"/> 不大於 0（<see cref="ArgumentOutOfRangeException"/>），
    /// 或選項互相衝突：例如 <see cref="ClaudeAgentOptions.ContinueConversation"/> 與 <see cref="ClaudeAgentOptions.ResumeSessionId"/> 同時指定、
    /// <see cref="ClaudeAgentOptions.ForkSession"/> 沒有要接續的 session、<see cref="ClaudeAgentOptions.CanUseTool"/> 與 <see cref="ClaudeAgentOptions.PermissionPromptToolName"/> 同時設定，
    /// 或 <see cref="ClaudeAgentOptions.Settings"/>、<see cref="ClaudeAgentOptions.SettingsFile"/>、<see cref="ClaudeAgentOptions.Sandbox"/> 互相重疊。
    /// </exception>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_state == ClientState.Closed, this);
            if (_state != ClientState.New)
            {
                throw new InvalidOperationException("這個 client 已經連線過；要重新開始請建立新的 ClaudeAgentClient。");
            }

            _state = ClientState.Connecting;
        }

        var protocol = new ControlProtocol(_transport, _options.CanUseTool)
        {
            InitializeOptions = _options,
            VerbatimPrompts = _options.VerbatimPrompts,
        };
        try
        {
            await _transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
            protocol.Start();
            ServerInfo = await protocol
                .InitializeAsync(InitializeTimeout(Environment.GetEnvironmentVariable("CLAUDE_CODE_STREAM_CLOSE_TIMEOUT")), cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            lock (_gate)
            {
                _state = ClientState.Closed;
            }

            await protocol.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        bool closedMeanwhile;
        lock (_gate)
        {
            closedMeanwhile = _state == ClientState.Closed;
            if (!closedMeanwhile)
            {
                _protocol = protocol;
                _state = ClientState.Connected;
            }
        }

        // 連線途中就被 DisposeAsync 了
        if (closedMeanwhile)
        {
            await protocol.DisposeAsync().ConfigureAwait(false);
            throw new ObjectDisposedException(GetType().FullName);
        }
    }

    /// <summary>送出一句話。不等回覆；回覆從 <see cref="ReceiveResponseAsync"/> 或 <see cref="ReceiveMessagesAsync"/> 讀。對應 Python 的 <c>query()</c>。</summary>
    public Task SendAsync(string prompt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return RequireProtocol().SendUserMessageAsync(prompt, cancellationToken);
    }

    /// <summary>
    /// 讀取 CLI 送來的每一則訊息，跨越多輪，直到 CLI 結束或 client 關閉。
    /// 同一時間只能有一個讀取者；多輪對話時可以一直讀，或每輪各用一次 <see cref="ReceiveResponseAsync"/>。
    /// </summary>
    /// <exception cref="CliProcessException">CLI 異常結束。</exception>
    /// <exception cref="MessageParseException">認得的訊息缺少必要欄位。</exception>
    public async IAsyncEnumerable<ClaudeMessage> ReceiveMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var protocol = RequireProtocol();
        await foreach (var data in protocol.ReceiveMessagesAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return MessageParser.Parse(data);
        }
    }

    /// <summary>讀到這一輪的 <see cref="ResultMessage"/> 為止，包含它。對應 Python 的 <c>receive_response()</c>。</summary>
    public async IAsyncEnumerable<ClaudeMessage> ReceiveResponseAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var message in ReceiveMessagesAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return message;
            if (message is ResultMessage)
            {
                yield break;
            }
        }
    }

    /// <summary>中斷這一輪。對話還在，之後可以接著 <see cref="SendAsync"/>；被中斷的那一輪仍會送來一個出錯的 <see cref="ResultMessage"/>。</summary>
    public Task InterruptAsync(CancellationToken cancellationToken = default) =>
        RequireProtocol().InterruptAsync(cancellationToken);

    /// <summary>
    /// 換模型：之後的回覆改用 <paramref name="model"/>（例如 <c>sonnet</c> 或完整的模型 ID），<see langword="null"/> 回到 Claude Code 的預設。
    /// 對話還在，不用重新連線。對應 Python 的 <c>set_model()</c>。
    /// 回傳 CLI 換過去的完整模型 ID（例如 <c>sonnet</c> 是 <c>claude-sonnet-5-5</c>），CLI 沒回報時是 <see langword="null"/>；
    /// Python 與 TypeScript SDK 不回傳這個值。CLI 也會送來一則重播的 <see cref="UserMessage"/>（原始 JSON 的 <c>isReplay</c> 是 <see langword="true"/>），
    /// 內容是 <c>&lt;local-command-stdout&gt;Set model to …&lt;/local-command-stdout&gt;</c>。
    /// </summary>
    public Task<string?> SetModelAsync(string? model = null, CancellationToken cancellationToken = default) =>
        RequireProtocol().SetModelAsync(model, cancellationToken);

    /// <summary>
    /// 對話中途套用設定，鍵與值跟 Claude Code 的 <c>settings.json</c> 一樣。設定合併進這個 session 的 flag 設定層
    /// （優先於使用者、專案、本機的設定檔，低於組織的管理設定），不寫進設定檔。
    /// 每次呼叫淺層合併最上層的鍵；值給 JSON 的 <c>null</c> 就清掉那個鍵，回到較低層的設定。
    /// 例如 <c>new JsonObject { ["effortLevel"] = "high" }</c> 換思考深度（也接受只在這個 session 有效的 <c>max</c>），
    /// <c>effortLevel</c> 給 <c>null</c> 回到模型預設的深度。
    /// Python SDK 沒有這個功能，對應 TypeScript SDK 的 <c>applyFlagSettings()</c>。
    /// </summary>
    public Task ApplyFlagSettingsAsync(JsonObject settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return RequireProtocol().ApplyFlagSettingsAsync(settings, cancellationToken);
    }

    /// <summary>
    /// 查目前 context window 的用量，依類別拆開，跟 CLI 的 <c>/context</c> 指令顯示的一樣。對應 Python 的 <c>get_context_usage()</c>。
    /// <paramref name="detail"/> 不指定時 CLI 用 <see cref="ContextUsageDetail.Full"/>；Python SDK 沒有這個參數，對應 TypeScript SDK 的 <c>detail</c>。
    /// 還沒說話也可以查。MCP server 在背景連線，剛連線就查的話可能還沒算進它們的工具。
    /// </summary>
    /// <exception cref="ClaudeAgentException">CLI 回應處理失敗，例如舊版 CLI 不支援。</exception>
    /// <exception cref="MessageParseException">回應缺少必要欄位或格式不對。</exception>
    /// <exception cref="TimeoutException">CLI 逾時沒有回應。</exception>
    public async Task<ContextUsage> GetContextUsageAsync(
        ContextUsageDetail? detail = null,
        CancellationToken cancellationToken = default)
    {
        var response = await RequireProtocol().GetContextUsageAsync(detail, cancellationToken).ConfigureAwait(false);
        return MessageParser.ParseContextUsage(response);
    }

    /// <summary>
    /// 換權限模式，之後的工具呼叫照新的模式判斷，不用重新連線。對應 Python 的 <c>set_permission_mode()</c>。
    /// 回傳 CLI 回報目前生效的模式，CLI 沒回報時是 <see langword="null"/>；Python 與 TypeScript SDK 不回傳這個值。
    /// 要切到 <see cref="PermissionMode.BypassPermissions"/>，開對話時要設定 <see cref="ClaudeAgentOptions.AllowDangerouslySkipPermissions"/>。
    /// </summary>
    /// <exception cref="ClaudeAgentException">CLI 拒絕，例如這個帳號不能用自動模式。</exception>
    public async Task<PermissionMode?> SetPermissionModeAsync(PermissionMode mode, CancellationToken cancellationToken = default)
    {
        var current = await RequireProtocol().SetPermissionModeAsync(mode.Value, cancellationToken).ConfigureAwait(false);
        return current is null ? null : new PermissionMode(current);
    }

    /// <summary>
    /// 把檔案還原到某則使用者訊息時的樣子。要開啟 <see cref="ClaudeAgentOptions.EnableFileCheckpointing"/>；
    /// 使用者訊息的 UUID 從 <see cref="UserMessage"/> 的 <see cref="ClaudeMessage.Uuid"/> 讀，要收到這些訊息，
    /// 用 <see cref="ClaudeAgentOptions.ExtraArguments"/> 帶 <c>replay-user-messages</c>。對應 Python 的 <c>rewind_files()</c>。
    /// <paramref name="dryRun"/> 是 <see langword="true"/> 時只回報會改哪些檔案，不真的還原；Python SDK 沒有這個參數，對應 TypeScript SDK 的 <c>dryRun</c>。
    /// </summary>
    /// <exception cref="MessageParseException">回應缺少必要欄位或格式不對。</exception>
    public async Task<RewindFilesResult> RewindFilesAsync(
        string userMessageId,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userMessageId);
        var response = await RequireProtocol().RewindFilesAsync(userMessageId, dryRun, cancellationToken).ConfigureAwait(false);
        return MessageParser.ParseRewindFilesResult(response);
    }

    /// <summary>
    /// 停止一個背景任務，任務 ID 從 <see cref="TaskStartedMessage.TaskId"/> 讀。對應 Python 的 <c>stop_task()</c>。
    /// 停止後 CLI 會送來狀態是結束的 <see cref="TaskUpdatedMessage"/>（<c>killed</c>），可能還有一則 <see cref="TaskNotificationMessage"/>（<c>stopped</c>）。
    /// </summary>
    public Task StopTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(taskId);
        return RequireProtocol().StopTaskAsync(taskId, cancellationToken);
    }

    /// <summary>
    /// 把前景的任務（Bash 指令與 subagent）移到背景，跟在終端機按 Ctrl+B 一樣：工具呼叫馬上回報「在背景執行」，這一輪繼續，任務做完時送 <see cref="TaskNotificationMessage"/>。
    /// <paramref name="toolUseId"/> 指定只移那個工具呼叫啟動的任務，回傳有沒有找到；不指定時移全部，回傳 <see langword="true"/>。
    /// Python SDK 沒有這個方法，對應 TypeScript SDK 的 <c>backgroundTasks()</c>。
    /// </summary>
    public async Task<bool> BackgroundTasksAsync(string? toolUseId = null, CancellationToken cancellationToken = default)
    {
        var response = await RequireProtocol().BackgroundTasksAsync(toolUseId, cancellationToken).ConfigureAwait(false);
        return !response.TryGetProperty("backgrounded", out var backgrounded) || backgrounded.ValueKind != JsonValueKind.False;
    }

    /// <summary>查所有 MCP server 的連線狀態。對應 Python 的 <c>get_mcp_status()</c>（Python 回傳包著清單的物件，這裡直接回傳清單）。</summary>
    /// <exception cref="MessageParseException">回應缺少必要欄位或格式不對。</exception>
    public async Task<IReadOnlyList<McpServerStatus>> GetMcpStatusAsync(CancellationToken cancellationToken = default)
    {
        var response = await RequireProtocol().GetMcpStatusAsync(cancellationToken).ConfigureAwait(false);
        return MessageParser.ParseMcpStatus(response);
    }

    /// <summary>重新連線一個斷線或連線失敗的 MCP server。對應 Python 的 <c>reconnect_mcp_server()</c>。</summary>
    /// <exception cref="ClaudeAgentException">重新連線失敗。</exception>
    public Task ReconnectMcpServerAsync(string serverName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(serverName);
        return RequireProtocol().ReconnectMcpServerAsync(serverName, cancellationToken);
    }

    /// <summary>
    /// 開啟或關閉一個 MCP server：關閉時斷線並移除它的工具，開啟時重新連線、工具也回來。對應 Python 的 <c>toggle_mcp_server()</c>。
    /// </summary>
    /// <exception cref="ClaudeAgentException">CLI 拒絕，例如沒有這個 server。</exception>
    public Task ToggleMcpServerAsync(string serverName, bool enabled, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(serverName);
        return RequireProtocol().ToggleMcpServerAsync(serverName, enabled, cancellationToken);
    }

    /// <summary>
    /// 整組換掉對話中動態加入的 MCP server：不在 <paramref name="servers"/> 裡的會移除，新的會連線。
    /// 開對話時的 <see cref="ClaudeAgentOptions.McpServers"/> 與設定檔裡的不受影響。
    /// Python SDK 沒有這個方法，對應 TypeScript SDK 的 <c>setMcpServers()</c>。
    /// </summary>
    /// <exception cref="MessageParseException">回應缺少必要欄位或格式不對。</exception>
    public async Task<McpSetServersResult> SetMcpServersAsync(
        IReadOnlyDictionary<string, McpServerConfig> servers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(servers);
        var response = await RequireProtocol().SetMcpServersAsync(servers, cancellationToken).ConfigureAwait(false);
        return MessageParser.ParseMcpSetServersResult(response);
    }

    /// <summary>
    /// 從已連線的 MCP server 讀一個 MCP Apps 的 UI 資源（<paramref name="uri"/> 必須是 <c>ui://</c>，通常是工具宣告的 <c>_meta.ui.resourceUri</c>）。
    /// 不會觸發模型。內容來自第三方（通常是 HTML），顯示時要放在沙箱裡。
    /// Python SDK 沒有這個方法，對應 TypeScript SDK 的 <c>readMcpResource()</c>。
    /// </summary>
    /// <exception cref="ClaudeAgentException">CLI 拒絕，例如不是 <c>ui://</c>、沒有這個 server，或 server 沒連上（先用 <see cref="ReconnectMcpServerAsync"/>）。</exception>
    /// <exception cref="MessageParseException">回應缺少必要欄位或格式不對。</exception>
    public async Task<IReadOnlyList<McpResourceContent>> ReadMcpResourceAsync(
        string serverName,
        string uri,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(serverName);
        ArgumentException.ThrowIfNullOrEmpty(uri);
        var response = await RequireProtocol().ReadMcpResourceAsync(serverName, uri, cancellationToken).ConfigureAwait(false);
        return MessageParser.ParseMcpResourceContents(response);
    }

    /// <summary>
    /// 固定一個 MCP server 的工具要用哪種權限模式判斷，<see langword="null"/> 清掉。只在這個 session 的模式原本會自動允許時才生效，
    /// 例如在 <see cref="PermissionMode.BypassPermissions"/> 下仍讓某個 server 的工具經過詢問或自動模式的判斷。
    /// CLI 只接受 <see cref="PermissionMode.Default"/>、<see cref="PermissionMode.Auto"/> 與 <see langword="null"/>，只能變得更嚴格。
    /// 回傳 CLI 的提醒，沒有時是 <see langword="null"/>。Python SDK 沒有這個方法，對應 TypeScript SDK 的 <c>setMcpPermissionModeOverride()</c>；
    /// CLI 把這個請求標成內部使用，之後可能改變。
    /// </summary>
    /// <exception cref="ClaudeAgentException">CLI 拒絕，例如給了其他模式。</exception>
    public async Task<string?> SetMcpPermissionModeOverrideAsync(
        string serverName,
        PermissionMode? mode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(serverName);
        var response = await RequireProtocol()
            .SetMcpPermissionModeOverrideAsync(serverName, mode?.Value, cancellationToken)
            .ConfigureAwait(false);
        return response.TryGetProperty("warning", out var warning) && warning.ValueKind == JsonValueKind.String ? warning.GetString() : null;
    }

    /// <summary>
    /// 從磁碟重新載入外掛，回傳重新載入後的指令、subagent、外掛與 MCP server。
    /// <paramref name="holdOnCacheImpact"/> 是 <see langword="true"/> 時，如果會改變工具清單、讓提示詞快取失效就不套用（<see cref="ReloadPluginsResult.Held"/>）；
    /// <paramref name="addPlugins"/> 是同時要多載入的外掛，已經載入的不會移除。
    /// Python SDK 沒有這個方法，對應 TypeScript SDK 的 <c>reloadPlugins()</c>。
    /// </summary>
    /// <exception cref="MessageParseException">回應缺少必要欄位或格式不對。</exception>
    public async Task<ReloadPluginsResult> ReloadPluginsAsync(
        bool holdOnCacheImpact = false,
        IReadOnlyList<LocalPlugin>? addPlugins = null,
        CancellationToken cancellationToken = default)
    {
        var response = await RequireProtocol()
            .ReloadPluginsAsync(holdOnCacheImpact, addPlugins ?? [], cancellationToken)
            .ConfigureAwait(false);
        return MessageParser.ParseReloadPluginsResult(response);
    }

    /// <summary>
    /// 從磁碟重新載入 skill（例如對話中途新增了 SKILL.md），回傳重新載入後的 skill。
    /// Python SDK 沒有這個方法，對應 TypeScript SDK 的 <c>reloadSkills()</c>。
    /// </summary>
    /// <exception cref="MessageParseException">回應缺少必要欄位或格式不對。</exception>
    public async Task<IReadOnlyList<SlashCommand>> ReloadSkillsAsync(CancellationToken cancellationToken = default)
    {
        var response = await RequireProtocol().ReloadSkillsAsync(cancellationToken).ConfigureAwait(false);
        return MessageParser.ParseReloadSkillsResult(response);
    }

    /// <summary>關閉：取消等待中的權限詢問、關閉 stdin 讓 CLI 寫完 session 後結束，逾時才強制結束。</summary>
    public async ValueTask DisposeAsync()
    {
        ControlProtocol? protocol;
        bool wasConnecting;
        lock (_gate)
        {
            if (_state == ClientState.Closed)
            {
                return;
            }

            wasConnecting = _state == ClientState.Connecting;
            _state = ClientState.Closed;
            protocol = _protocol;
            _protocol = null;
        }

        if (protocol is not null)
        {
            await protocol.DisposeAsync().ConfigureAwait(false);
        }
        else if (!wasConnecting)
        {
            // 從沒連線過；連線中的話由 ConnectAsync 自己收尾
            await _transport.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>initialize 的等待上限：至少 60 秒，環境變數設得更長就照它。</summary>
    internal static TimeSpan InitializeTimeout(string? streamCloseTimeoutMs) =>
        int.TryParse(streamCloseTimeoutMs, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds)
        && TimeSpan.FromMilliseconds(milliseconds) > MinimumInitializeTimeout
            ? TimeSpan.FromMilliseconds(milliseconds)
            : MinimumInitializeTimeout;

    private ControlProtocol RequireProtocol()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_state == ClientState.Closed, this);
            return _protocol ?? throw new CliConnectionException("還沒連上 Claude Code，請先呼叫 ConnectAsync。");
        }
    }
}
