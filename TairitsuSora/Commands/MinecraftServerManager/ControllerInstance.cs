using System.Collections.Specialized;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using LanguageExt.UnitsOfMeasure;
using RateLimiter;
using Sora.Entities.Segment.DataModel;
using Sora.Enumeration;
using Sora.Enumeration.ApiType;
using Sora.EventArgs.SoraEvent;
using TairitsuSora.Core;
using TairitsuSora.TairitsuSora.Commands.MinecraftServerManager;
using TairitsuSora.Utils;

namespace TairitsuSora.Commands.MinecraftServerManager;

public class ControllerInstance : IDisposable
{
    public ControllerConfig Config { get; private set; }
    public long GroupId { get; }

    public bool EnableMessageForwarding
    {
        get => Config.EnableMessageForwarding;
        set
        {
            Config = Config with { EnableMessageForwarding = value };
            _minecraftMessagePoller.Enabled = value;
        }
    }

    public bool EnableDailyBackup
    {
        get => Config.BackupConfig?.EnableDailyBackup ?? false;
        set
        {
            if (Config.BackupConfig is null) return;
            Config = Config with { BackupConfig = Config.BackupConfig with { EnableDailyBackup = value } };
            _dailyBackupController.Enabled = value;
        }
    }

    public ControllerInstance(ControllerConfig config, long groupId, HttpClient client)
    {
        Config = config;
        GroupId = groupId;
        var throttler = TimeLimiter.GetFromMaxCountByInterval(1, 0.5.Seconds());
        _client = new HttpClientWrapper(client, throttler, Token);
        _whitelistedPlayers = new AsyncCache<HashSet<string>>(GetWhitelistedPlayers, 8.Hours());
        _minecraftMessagePoller = new TaskLooper(SyncMinecraftMessageToGroup, Token);
        _minecraftMessagePoller.Enabled = EnableMessageForwarding;
        _dailyBackupController = new TaskLooper(AutoBackup, Token);
        _dailyBackupController.Enabled = EnableDailyBackup;
    }

    public async ValueTask Run() => await Task.WhenAll(
        SyncGroupMessageToMinecraft(),
        _minecraftMessagePoller.Run().AsTask(),
        _dailyBackupController.Run().AsTask()
    );

    public async ValueTask<PingData> Ping(int maxRetries = 3)
    {
        int retry = 0;
        while (true)
        {
            var data = await PingOnce();
            if (data is { Status: not PingStatusCode.Running }) return data;
            // Sometimes the API returns 0.00% as the CPU usage, usually requerying it
            // should resolve the problem.
            if (data.ProcessInfo!.CpuUsage >= 0.01f || retry++ >= maxRetries) return data;
            await Task.Delay(2.Seconds(), Token);
        }
    }

    public async ValueTask<string> GetSeed()
    {
        string seed = "";
        bool MatchSeed(string line)
        {
            var match = SeedOutputPattern.Match(line);
            if (!match.Success) return false;
            seed = match.Groups[1].Value;
            return true;
        }
        await ExpectServerInfoLog(MatchSeed, 10.Seconds(), "seed");
        return seed;
    }

    public async ValueTask MakeBackup()
    {
        if (Config.BackupConfig is null)
            throw new InvalidOperationException("无备份配置");

        string worldDir = Config.BackupConfig.WorldDirectory;
        string backupDir = Config.BackupConfig.BackupDirectory;
        long maxTotalSize = Config.BackupConfig.MaxTotalSize;
        string zipPath = PathUtils.Combine(backupDir, $"{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        await ExpectServerInfoLog(["Automatic saving is now disabled", "Saving is already turned off"], 10.Seconds(), "save-off");
        await ExpectServerInfoLog(["Saved the game"], 2.Minutes(), "save-all");
        await ExpectServerInfoLog(["Automatic saving is now enabled", "Saving is already turned on"], 10.Seconds(), "save-on");
        await EnsureBackupDirectoryExists(backupDir);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Token);
        BackupStatus status = new(0, 0, 0);

        async Task CompressTask(CancellationTokenSource src)
        {
            await CompressFiles(worldDir, zipPath, 5.Minutes());
            await src.CancelAsync();
        }

        async Task PruneTask(CancellationToken token)
        {
            async ValueTask Work(bool delay)
            {
                var s = await PruneOldBackups(backupDir, maxTotalSize);
                status = s with { PrunedCount = status.PrunedCount + s.PrunedCount };
                if (delay) await Task.Delay(10.Seconds(), token);
            }
            await AsyncExtensions.LoopUntilCancellation(() => Work(true));
            await Work(false); // Need to prune one last time after the compression has completed
        }

        await Task.WhenAll(CompressTask(cts), PruneTask(cts.Token));
        const long giga = 1_000_000_000;
        string msg = $"备份完成！留存 {status.CurrentCount} 个文件，删除旧文件 {status.PrunedCount} 个，" +
                     $"文件共占 {(double)status.TotalSize / giga:0.00}GB";
        await SendGroupMessage(msg);
    }

    public async ValueTask RestartServer(bool reportWhenDone = false, bool forced = false)
    {
        async ValueTask<bool> EnsureServerRunning()
        {
            var ping = await Ping();
            if (ping.Status == PingStatusCode.Running) return true;
            if (reportWhenDone)
                await SendGroupMessage(ping.Status switch
                {
                    PingStatusCode.Stopped => "服务器已停止",
                    PingStatusCode.Stopping => "服务器正在关闭",
                    PingStatusCode.Starting => "服务器正在启动",
                    _ => "由于未知原因"
                } + "，此时无法完成重启");
            return false;
        }

        async ValueTask<bool> WaitServerStatus(PingStatusCode expectedStatus) =>
            await AsyncExtensions.RetryUntil(async () => (await Ping()).Status == expectedStatus,
                5.Minutes(), 5.Seconds(), Token);

        async ValueTask<bool> CheckNoPlayerOnline() => (await Ping()).Info?.OnlinePlayerCount is 0 or null;

        ValueTask SendRequest(string api) =>
            _client.Get(new UriBuilder($"{Config.ApiEndpoint}/api/protected_instance/{api}")
            { Query = TemplateQuery.ToString() }.Uri);

        async ValueTask DoRestart()
        {
            try
            {
                await SendRequest("restart");
                return;
            }
            catch (Exception) { if (!forced) throw; }

            // Forced and failed to restart
            await SendRequest("stop").IgnoreException(false);
            if (!await WaitServerStatus(PingStatusCode.Stopped))
                await SendGroupMessage("强制重启时服务器未能在五分钟内关闭，请检查服务器运行状态");
            await SendRequest("open");
        }

        if (!forced)
        {
            if (!await EnsureServerRunning()) return;
            if (!await CheckNoPlayerOnline())
            {
                await SendMessage("<Server> 服务器准备重启，请尽快停止正在运行的机械并下线，" +
                                  "以免在重启过程中导致机械故障，谢谢配合。请注意即使仍有玩家在线，五分钟后会强制重启。");
                await AsyncExtensions.RetryUntil(CheckNoPlayerOnline, 5.Minutes(), 5.Seconds(), Token);
            }
        }
        await DoRestart();

        if (!await WaitServerStatus(PingStatusCode.Running))
            await SendGroupMessage("已发送服务端重启请求，但五分钟内服务器未能正常启动，请检查服务器运行状态");
        else if (!await ExpectServerInfoLog(line => ServerDoneStartingPattern.Match(line).Success, 5.Minutes())
                     .ExceptionAsFalse(logException: false))
            await SendGroupMessage("服务端正在启动，但五分钟内未能完成准备，请检查服务器运行状态");
        else if (reportWhenDone)
            await SendGroupMessage("服务端重启完毕！");
    }

    public void Dispose() => _src.Dispose();

    private record BackupStatus(long TotalSize, int CurrentCount, int PrunedCount);

    private const RegexOptions RegOptions =
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.NonBacktracking;
    private static readonly Regex ServerInfoPattern = new(
        @"\[\d{2}:\d{2}:\d{2}\] \[Server thread/INFO\]: (.*)", RegOptions);
    private static readonly Regex MessagePattern = new(@"<\w+> .*", RegOptions);
    private static readonly Regex SeedOutputPattern = new(@"Seed: \[(.*)\]", RegOptions);
    private static readonly Regex ServerDoneStartingPattern = new(@"Done \(.*s\)! For help, type ""help""", RegOptions);
    private static readonly Regex WhitelistedPlayersPattern = new(@"There are \d+ whitelisted player\(s\): (.*)", RegOptions);

    // MCSM has a rate limit of 40ms per request. We apply a delay between the requests here to avoid that.
    private CancellationTokenSource _src =
        CancellationTokenSource.CreateLinkedTokenSource(Application.Instance.CancellationToken);
    private HttpClientWrapper _client;
    private readonly AsyncCache<HashSet<string>> _whitelistedPlayers;
    private readonly TaskLooper _minecraftMessagePoller;
    private readonly TaskLooper _dailyBackupController;
    private readonly OutputLogStream _chatStream = new();

    private CancellationToken Token => _src.Token;

    private NameValueCollection TemplateQuery
    {
        get
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["apikey"] = Config.ApiKey;
            query["uuid"] = Config.Uuid;
            query["remote_uuid"] = Config.RemoteUuid;
            return query;
        }
    }

    private async ValueTask<PingData> PingOnce()
    {
        var url = new UriBuilder($"{Config.ApiEndpoint}/api/instance")
        { Query = TemplateQuery.ToString() }.Uri;
        return await _client.GetFromJson<PingData>(url);
    }

    private async ValueTask<string[]> GetOutputLog()
    {
        var query = TemplateQuery;
        query["size"] = "4096";
        var url = new UriBuilder($"{Config.ApiEndpoint}/api/protected_instance/outputlog")
        { Query = query.ToString() }.Uri;
        return (await _client.GetFromJson<string>(url))
            .Split("\n")
            .Skip(1)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    private async Task SyncGroupMessageToMinecraft()
    {
        async ValueTask SendGroupMessageToMinecraft(string _, GroupMessageEventArgs eventArgs)
        {
            if (eventArgs.SourceGroup.Id != GroupId || !_minecraftMessagePoller.Enabled) return;
            string senderName = eventArgs.SenderInfo.Card.EmptyAsNull() ?? eventArgs.SenderInfo.Nick;
            var body = eventArgs.Message.MessageBody;
            if (body.ElementAtOrDefault(0) is not { MessageType: SegmentType.Reply } segment ||
                await Application.Api.GetMessage(((ReplySegment)segment.Data).Target) is not
                {
                    apiStatus.RetCode: ApiStatusType.Ok,
                    message: { } replied,
                    sender: { } repliedSender,
                    isGroupMsg: true
                })
            {
                await SendMessage($"<{senderName}> {eventArgs.Message.MessageBody.Stringify()}");
                return;
            }
            int start = body.ElementAtOrDefault(1) is { Data: AtSegment at } &&
                        at.Target == repliedSender.Id.ToString() ? 2 : 1;
            string repliedSenderName = "";
            if (repliedSender.Id != Application.Instance.SelfId)
            {
                var member = (await Application.Api.GetGroupMemberInfo(eventArgs.SourceGroup.Id, repliedSender.Id)).memberInfo;
                repliedSenderName = $"<{member.Card.EmptyAsNull() ?? member.Nick}> ";
            }
            await SendMessage($"<{senderName}> Re: {repliedSenderName}{replied.MessageBody.Stringify()}\n" +
                              eventArgs.Message.MessageBody.Stringify(skip: start));
        }

        Application.Service.Event.OnGroupMessage += SendGroupMessageToMinecraft;
        try { await _src.Token.WaitUntilCanceled(); }
        finally { Application.Service.Event.OnGroupMessage -= SendGroupMessageToMinecraft; }
    }

    private async ValueTask SyncMinecraftMessageToGroup(CancellationToken token)
    {
        if (await GetOutputLog().ExceptionAsNull(logException: false) is not { } log)
        {
            await Task.Delay(2.Seconds(), token);
            return;
        }
        (bool wasEmpty, string[] lines) = _chatStream.UpdateLines(log);
        if (!wasEmpty && lines.Length != 0)
        {
            List<string> messages = [];
            HashSet<string>? players = null;
            foreach (string line in lines)
            {
                var match = ServerInfoPattern.Match(line);
                if (!match.Success) continue;
                var text = match.Groups[1].Value;
                if (!MessagePattern.IsMatch(text))
                {
                    players ??= await _whitelistedPlayers.Get();
                    if (text.Split(' ', 2) is not [var name, var rest] ||
                        !players.Contains(name) ||
                        rest.Contains("lost connection") ||
                        rest.Contains("joined the game") ||
                        rest.Contains("left the game"))
                        continue;
                }
                messages.Add(text);
            }
            if (messages.Count > 0)
                await SendGroupMessage(string.Join("\n", messages));
        }
        await Task.Delay(5.Seconds(), token);
    }

    private async ValueTask AutoBackup(CancellationToken token)
    {
        DateTime nextBackup = DateTime.Today.AddHours(5); // 5:00 today
        if (nextBackup <= DateTime.Now) nextBackup = nextBackup.AddDays(1); // 5:00 tomorrow
        await Task.Delay(nextBackup - DateTime.Now, token);
        await MakeBackup();
    }

    /// <summary>
    /// Run a command in the Minecraft server.
    /// This is a fire-and-forget command, so no response is returned.
    /// </summary>
    private async ValueTask RunCommand(string command)
    {
        var query = TemplateQuery;
        query["command"] = command;
        var url = new UriBuilder($"{Config.ApiEndpoint}/api/protected_instance/command")
        { Query = query.ToString() }.Uri;
        await _client.Get(url);
    }

    /// <summary>
    /// Check the output log for a specific pattern, and wait until it appears.
    /// Optionally run a command before checking the output.
    /// </summary>
    /// <param name="outputMatcher">Predicate to check the output.</param>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <param name="command">The optional command to run.</param>
    private async ValueTask ExpectServerInfoLog(
        Func<string, bool> outputMatcher, TimeSpan timeout, string? command = null)
    {
        DateTime deadline = DateTime.Now + timeout;
        OutputLogStream log = new();
        log.UpdateLines(await GetOutputLog());
        if (command is not null) await RunCommand(command);

        bool FilteredMatch(string line)
        {
            var match = ServerInfoPattern.Match(line);
            if (!match.Success) return false;
            var text = match.Groups[1].Value.Trim();
            return outputMatcher(text);
        }

        async ValueTask<bool> CheckLog()
            => log.UpdateLines(await GetOutputLog()).lines.Any(FilteredMatch);

        await RetryUntil(CheckLog, deadline);
    }

    private ValueTask ExpectServerInfoLog(
        IEnumerable<string> expectedOutput, TimeSpan timeout, string? command = null)
        => ExpectServerInfoLog(expectedOutput.Contains, timeout, command);

    private ValueTask SendMessage(string message)
    {
        Dictionary<string, string> json = new() { ["text"] = message };
        return RunCommand($"tellraw @a {JsonSerializer.Serialize(json)}");
    }

    private async ValueTask SendGroupMessage(string message) =>
        await Application.Api.SendGroupMessage(GroupId, message);

    private async ValueTask<List<FileItemData>> ListDirectory(string path)
    {
        const int pageSize = 40;

        ValueTask<ListFileData> ProbeList(int page)
        {
            var query = TemplateQuery;
            query["target"] = path;
            query["page"] = page.ToString();
            query["page_size"] = pageSize.ToString();
            query["file_name"] = "";
            var url = new UriBuilder($"{Config.ApiEndpoint}/api/files/list")
            { Query = query.ToString() }.Uri;
            return _client.GetFromJson<ListFileData>(url);
        }

        var initial = await ProbeList(0);
        List<FileItemData> result = initial.Items.ToList();
        int totalPages = (int)Math.Ceiling((double)initial.Total / pageSize);
        for (int i = 1; i < totalPages; i++)
            result.AddRange((await ProbeList(i)).Items);
        return result;
    }

    private async ValueTask<FileStatusData> GetFileStatus()
    {
        var url = new UriBuilder($"{Config.ApiEndpoint}/api/files/status")
        { Query = TemplateQuery.ToString() }.Uri;
        return await _client.GetFromJson<FileStatusData>(url);
    }

    private async ValueTask EnsureBackupDirectoryExists(string path)
    {
        var url = new UriBuilder($"{Config.ApiEndpoint}/api/files/mkdir")
        { Query = TemplateQuery.ToString() }.Uri;
        // Swallow the possible 500 error when the path already exists
        await _client.Post(url, JsonContent($$"""{"target": "{{path}}"}""")).IgnoreException(logException: false);
    }

    private async ValueTask CompressFiles(string inPath, string zipPath, TimeSpan timeout)
    {
        DateTime deadline = DateTime.Now + timeout;
        var url = new UriBuilder($"{Config.ApiEndpoint}/api/files/compress")
        { Query = TemplateQuery.ToString() }.Uri;
        await _client.Post(url, JsonContent($$"""
            {
                "type": 1,
                "source": "{{zipPath}}",
                "targets": ["{{inPath}}"],
                "code": "utf-8"
            }
            """));
        await RetryUntil(async () => (await GetFileStatus()).InstanceFileTask == 0, deadline);
    }

    private async ValueTask<BackupStatus> PruneOldBackups(string path, long maxTotalSize)
    {
        var files = await ListDirectory(path);
        files.Sort((a, b) => -string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        long size = 0;
        for (int i = 0; i < files.Count; i++)
        {
            size += files[i].Size;
            if (size <= maxTotalSize) continue;
            string paths = string.Join(',',
                files.Skip(i).Select(f => $"\"{PathUtils.Combine(path, f.Name)}\""));
            var url = new UriBuilder($"{Config.ApiEndpoint}/api/files")
            { Query = TemplateQuery.ToString() }.Uri;
            await _client.Delete(url, JsonContent($$"""{"targets": [{{paths}}]}"""));
            return new BackupStatus(size - files[i].Size, i, files.Count - i);
        }
        return new BackupStatus(size, files.Count, 0);
    }

    private static HttpContent JsonContent(string json)
        => new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);

    private async ValueTask RetryUntil(
        Func<ValueTask<bool>> action, DateTime deadline, TimeSpan? firstIter = null, float growthFactor = 1.5f)
    {
        TimeSpan thisIter = firstIter ?? 1.Seconds();
        while (true)
        {
            TimeSpan remaining = deadline - DateTime.Now;
            if (remaining <= TimeSpan.Zero) throw new TimeoutException();
            if (remaining <= thisIter) thisIter = remaining;
            if (await action()) break;
            try { await Task.Delay(thisIter, Token); }
            catch (TaskCanceledException) { throw new TimeoutException(); }
            thisIter *= growthFactor;
        }
    }

    private async ValueTask<HashSet<string>> GetWhitelistedPlayers()
    {
        string list = "";
        bool MatchSeed(string line)
        {
            var match = WhitelistedPlayersPattern.Match(line);
            if (!match.Success) return false;
            list = match.Groups[1].Value;
            return true;
        }
        await ExpectServerInfoLog(MatchSeed, 10.Seconds(), "whitelist list");
        return list.Split(',').Select(s => s.Trim()).ToHashSet();
    }
}
