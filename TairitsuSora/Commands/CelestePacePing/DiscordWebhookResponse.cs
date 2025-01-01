using JetBrains.Annotations;

namespace TairitsuSora.Commands.CelestePacePing;

[UsedImplicitly]
public record DiscordWebhookResponse(
    string Id,
    int Type,
    string Content,
    string ChannelId,
    DiscordWebhookResponse.User Author,
    List<object> Attachments,
    List<DiscordWebhookRequest.Embed> Embeds,
    List<object> Mentions,
    List<object> MentionRoles,
    bool Pinned,
    bool MentionEveryone,
    bool Tts,
    DateTimeOffset Timestamp,
    DateTimeOffset? EditedTimestamp,
    int Flags,
    List<object> Components,
    string? WebhookId
    )
{
    [UsedImplicitly]
    public record User(
        string Id,
        string Username,
        string Discriminator,
        string? GlobalName = null,
        string? Avatar = null,
        int PublicFlags = 0,
        int Flags = 0,
        bool Bot = false
    );
}
