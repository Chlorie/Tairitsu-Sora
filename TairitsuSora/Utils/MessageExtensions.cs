using Sora.Entities;
using Sora.Entities.Info;
using Sora.Entities.Segment;
using Sora.Entities.Segment.DataModel;
using Sora.Enumeration;
using Sora.Enumeration.EventParamsType;
using Sora.EventArgs.SoraEvent;
using TairitsuSora.Core;

namespace TairitsuSora.Utils;

public static class MessageExtensions
{
    public static string Stringify(this SoraSegment segment) => segment.MessageType switch
    {
        SegmentType.Unknown => "[未知]",
        SegmentType.Ignore => "[忽略]",
        SegmentType.Text => segment.GetText()!,
        SegmentType.Face => "[表情]",
        SegmentType.Image => "[图片]",
        SegmentType.Record => "[语音]",
        SegmentType.Video => "[视频]",
        SegmentType.Music => "[音乐分享]",
        SegmentType.At => "@" + ((AtSegment)segment.Data).Target,
        SegmentType.Share => "[链接分享]",
        SegmentType.Reply => "[引用回复]",
        SegmentType.Forward => "[合并转发]",
        SegmentType.Poke => "[戳一戳]",
        SegmentType.Xml => "[XML 消息]",
        SegmentType.Json => "[JSON 消息]",
        SegmentType.RedBag => "[红包]",
        SegmentType.CardImage => "[大图]",
        SegmentType.TTS => "[语音转文字]",
        SegmentType.RPS => "[猜拳]",
        _ => throw new ArgumentOutOfRangeException()
    };

    public static string Stringify(this MessageBody msg, int skip = 0)
        => string.Concat(msg.Skip(skip).Select(seg => seg.Stringify()));

    public static string? GetText(this SoraSegment segment)
        => segment.MessageType != SegmentType.Text ? null : ((TextSegment)segment.Data).Content;

    public static string? GetIfOnlyText(this MessageBody msg)
        => msg.Count switch { 0 => "", 1 => msg[0].GetText(), _ => null };

    public static bool IsEmpty(this MessageBody msg)
        => msg.Count == 0 || (msg.Count == 1 && msg[0].Data is TextSegment { Content: "" });

    public static bool IsWhitespace(this MessageBody msg)
        => msg.Count == 0 ||
           (msg.Count == 1 &&
            msg[0].Data is TextSegment { Content: var content }
            && string.IsNullOrWhiteSpace(content));

    public static MessageBody TrimStart(this MessageBody msg)
    {
        if (msg.Count == 0) return msg;
        msg = new MessageBody(msg.ToList());
        if (msg[0].Data is not TextSegment first) return msg;
        string firstStr = first.Content.TrimStart();
        if (firstStr == "") msg.RemoveAt(0);
        else msg[0] = firstStr;
        return msg;
    }

    public static MessageBody TrimEnd(this MessageBody msg)
    {
        if (msg.Count == 0) return msg;
        msg = new MessageBody(msg.ToList());
        if (msg[0].Data is not TextSegment first) return msg;
        string firstStr = first.Content.TrimStart();
        if (firstStr == "") msg.RemoveAt(0);
        else msg[0] = firstStr;
        return msg;
    }

    public static MessageBody Trim(this MessageBody msg) => msg.TrimEnd().TrimStart();

    public static (SoraSegment token, MessageBody remaining) SeparateFirstToken(this MessageBody msg)
    {
        if (msg.IsEmpty()) return ("", msg);
        MessageBody res = new(msg.ToList());
        if (msg[0].Data is TextSegment { Content: var content })
        {
            string[] parts = content.SplitByWhitespaces(2);
            if (parts.Length == 2)
                res[0] = parts[1];
            else // parts.Length == 1
                res.RemoveAt(0);
            return (parts[0], res);
        }
        else
        {
            SoraSegment token = res[0];
            res.RemoveAt(0);
            while (res.Count > 0 && res[0].GetText() is { } text && string.IsNullOrWhiteSpace(text))
                res.RemoveAt(0);
            return (token, res);
        }
    }

    public static MessageBody Text(this MessageBody msg, string text)
    {
        msg.AddText(text);
        return msg;
    }

    public static MessageBody At(this MessageBody msg, long userId, string? name = null)
    {
        msg.Add(name is null ? SoraSegment.At(userId) : SoraSegment.At(userId, name));
        return msg;
    }

    public static MessageBody Image(this MessageBody msg, byte[] bytes)
    {
        using MemoryStream stream = new(bytes);
        msg.Add(SoraSegment.Image(stream));
        return msg;
    }

    public static MessageBody Record(this MessageBody msg, byte[] bytes)
    {
        using MemoryStream stream = new(bytes);
        msg.Add(SoraSegment.Record(stream.StreamToBase64()));
        return msg;
    }

    public static ValueTask<(ApiStatus apiStatus, int messageId)> QuoteReply(
        this GroupMessageEventArgs eventArgs, MessageBody msg)
        => eventArgs.Reply(SoraSegment.Reply(eventArgs.Message.MessageId) + msg);

    public static bool FromSameMember(this GroupMessageEventArgs self, GroupMessageEventArgs other)
        => self.SourceGroup.Id == other.SourceGroup.Id && self.SenderInfo.UserId == other.SenderInfo.UserId;

    public static bool FromSameGroup(this GroupMessageEventArgs self, GroupMessageEventArgs other)
        => self.SourceGroup.Id == other.SourceGroup.Id;

    public static bool IsAdmin(this GroupSenderInfo sender)
        => sender.Role >= MemberRoleType.Admin || Application.Instance.Admins.Contains(sender.UserId);

    public static string CardOrNick(this GroupSenderInfo sender)
        => string.IsNullOrEmpty(sender.Card) ? sender.Nick : sender.Card;
}
