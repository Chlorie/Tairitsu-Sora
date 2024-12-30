namespace TairitsuSora.Commands.CelestePacePing;

public record DiscordWebhookResponse(
    string Id,
    int Type,
    string Content,
    string ChannelId,
    DiscordWebhookResponse.AuthorType Author,
    List<object> Attachments,
    List<DiscordWebhookRequest.Embed> Embeds,
    List<object> Mentions,
    List<object> MentionRoles,
    bool Pinned,
    bool MentionEveryone,
    bool Tts,
    DateTimeOffset Timestamp,
    object EditedTimestamp,
    int Flags,
    List<object> Components,
    string WebhookId
    )
{
    public record AuthorType(
        string Id,
        string Username,
        object Avatar,
        string Discriminator,
        int PublicFlags,
        int Flags,
        bool Bot
    );
}
