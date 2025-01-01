using System.Diagnostics.CodeAnalysis;

namespace TairitsuSora.Commands.CelestePacePing;

[SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public record DiscordWebhookRequest(
    string Username,
    string? AvatarUrl,
    string Content,
    List<DiscordWebhookRequest.Embed> Embeds,
    DiscordWebhookRequest.AllowedMention AllowedMentions)
{
    public record Embed(
        Author? Author,
        string Title,
        string? Url,
        string? Description,
        int Color,
        List<Field> Fields,
        Thumbnail? Thumbnail,
        Image? Image,
        Footer? Footer
    );

    public record Author(string Name, string Url, string IconUrl);
    public record Field(string Name, string Value, bool Inline);
    public record Thumbnail(string Url);
    public record Image(string Url);
    public record Footer(string Text, string IconUrl);
    public record AllowedMention(List<string> Parse);
}
