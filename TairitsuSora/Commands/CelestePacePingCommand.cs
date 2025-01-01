using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Sora.Entities;
using Sora.EventArgs.SoraEvent;
using TairitsuSora.Commands.CelestePacePing;
using TairitsuSora.Core;
using TairitsuSora.Utils;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class CelestePacePingCommand : Command
{
    public override CommandInfo Info => new()
    {
        Trigger = "ctpp",
        Summary = "炼金？",
        Description =
            "提供假 Discord Webhook 接口，用于接入 Consistency Tracker 的 Pace Ping 功能，实时向群内播报蔚蓝炼金进度。"
    };

    public override ValueTask ApplyConfigAsync(JsonNode config)
    {
        _config = config.Deserialize<CommandConfig>();
        return ValueTask.CompletedTask;
    }

    public override ValueTask<JsonNode?> CollectConfigAsync()
        => ValueTask.FromResult(JsonSerializer.SerializeToNode(_config));

    public override async ValueTask InitializeAsync()
    {
        _server = BuildServer();
        if (_server is null) return;
        var text = await File.ReadAllTextAsync("data/joypixels.raw.json");
        _converter = EmojiShortcodeConverter.LoadFromMap(
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(text)!);
    }

    public override async ValueTask ExecuteAsync()
    {
        var token = Application.Instance.CancellationToken;
        if (_config is null)
            await token.WaitUntilCanceled();
        else
            await _server!.RunAsync(token);
    }

    [MessageHandler(
        Signature = "bind $name",
        Description = "绑定新的接口。[name] 为玩家显示名，置空则默认采用群名片。")]
    public async ValueTask<string> BindNewPing(GroupMessageEventArgs ev, string? name)
    {
        if (_config is null) return "未配置 Pace Ping 服务器，本功能不可用。";
        name ??= ev.SenderInfo.CardOrNick();
        long groupId = ev.SourceGroup.Id, userId = ev.Sender.Id;
        string token = GenerateRandomToken();
        bool added = _config.Pings.TryAdd(new MemberId(groupId, userId), new PingConfig(token, name));
        if (!added) return "已在本群绑定过 Pace Ping 服务，请勿重复绑定。";
        Uri uri = new(_config.DisplayUri, $"pacePing/{ev.SourceGroup.Id}-{ev.Sender.Id}/{token}/");
        var (_, friends) = await Application.Api.GetFriendList();
        bool isFriend = friends.Any(f => f.UserId == ev.Sender.Id);
        if (isFriend)
            await ev.Sender.SendPrivateMessage($"Webhook URL: {uri}");
        else
            await Application.Api.SendTemporaryMessage(userId, groupId, $"Webhook URL: {uri}");
        return "绑定成功，接口 URL 已私聊发送。";
    }

    [MessageHandler(Signature = "unbind", Description = "解绑接口。")]
    public string UnbindPing(GroupMessageEventArgs ev)
    {
        if (_config is null) return "未配置 Pace Ping 服务器，本功能不可用。";
        return _config.Pings.TryRemove(new MemberId(ev.SourceGroup.Id, ev.Sender.Id), out _)
            ? "解绑成功。"
            : "您未在本群绑定过 Pace Ping 服务。";
    }

    [MessageHandler(Signature = "rename $name", Description = "修改玩家显示名为 [name]。")]
    public string Rename(GroupMessageEventArgs ev, string name)
    {
        if (_config is null) return "未配置 Pace Ping 服务器，本功能不可用。";
        return _config.Pings.TryUpdate(new MemberId(ev.SourceGroup.Id, ev.Sender.Id),
            (_, old) => old with { UserName = name }).IsSome
            ? "修改成功。"
            : "您未在本群绑定过 Pace Ping 服务。";
    }

    [UsedImplicitly]
    private record CommandConfig(
        Uri ServiceUri,
        Uri DisplayUri,
        [property: JsonConverter(typeof(PingsJsonConverter))]
        ConcurrentDictionary<MemberId, PingConfig> Pings
    );

    private class PingsJsonConverter : JsonConverter<ConcurrentDictionary<MemberId, PingConfig>>
    {
        public override ConcurrentDictionary<MemberId, PingConfig> Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(JsonSerializer.Deserialize<List<KeyValuePair<MemberId, PingConfig>>>(ref reader, options) ?? []);

        public override void Write(Utf8JsonWriter writer,
            ConcurrentDictionary<MemberId, PingConfig> value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.ToList(), options);
    }

    private record PingConfig(
        string Token,
        string UserName
    );

    private class PingProcessor(CelestePacePingCommand parent) : IDiscordRequestProcessor
    {
        public async ValueTask<IActionResult> ProcessRequest(
            MemberId member, string token, string? patchedId, DiscordWebhookRequest request)
        {
            if (!Pings.TryGetValue(member, out var ping) || ping.Token != token)
                return new BadRequestResult();
            var (_, msgId) = await Application.Api.SendGroupMessage(member.GroupId, CreateMessage(request, ping.UserName));
            if (patchedId is not null && int.TryParse(patchedId, out int prevMsg))
                await Application.Api.RecallMessage(prevMsg);
            return new OkObjectResult(CreateResponse(msgId, member, request));
        }

        private ConcurrentDictionary<MemberId, PingConfig> Pings => parent._config!.Pings;

        private MessageBody CreateMessage(DiscordWebhookRequest request, string userName)
        {
            var converter = parent._converter!;
            string title = $"{request.Username} - {userName}".ToSansBoldItalicScript();
            string content = converter.Convert(request.Content);
            using EmbedRenderer renderer = new(converter);
            MessageBody body = [$"{title}\n{content}\n"];
            foreach (var embed in request.Embeds)
                body.Image(renderer.Render(embed));
            return body;
        }

        private static DiscordWebhookResponse CreateResponse(
            int msgId, MemberId member, DiscordWebhookRequest request) =>
            new(
                Id: msgId.ToString(), Type: 0, Content: request.Content, ChannelId: member.GroupId.ToString(),
                Author: new DiscordWebhookResponse.User("", request.Username, ""),
                Embeds: request.Embeds, Attachments: [], Mentions: [], MentionRoles: [], Pinned: false,
                MentionEveryone: false, Tts: false,
                Timestamp: DateTimeOffset.UtcNow, EditedTimestamp: DateTimeOffset.UtcNow,
                Flags: 0, Components: [], WebhookId: null
            );
    }

    private CommandConfig? _config;
    private WebApplication? _server;
    private EmojiShortcodeConverter? _converter;

    private WebApplication? BuildServer()
    {
        if (_config is null) return null;
        var builder = WebApplication.CreateBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.UseUrls(_config.ServiceUri.ToString());
        builder.Services
            .AddScoped<IDiscordRequestProcessor>(_ => new PingProcessor(this))
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
                options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
            });
        var webApp = builder.Build();
        webApp.MapControllers();
        return webApp;
    }

    private string GenerateRandomToken()
    {
        byte[] token = new byte[16];
        RandomNumberGenerator.Fill(token);
        return Base64UrlTextEncoder.Encode(token.ToArray());
    }
}
