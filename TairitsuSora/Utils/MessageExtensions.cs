using Sora.Entities;
using Sora.Entities.Segment;
using Sora.Entities.Segment.DataModel;
using Sora.Enumeration;
using Sora.EventArgs.SoraEvent;

namespace TairitsuSora.Utils;

public static class MessageExtensions
{
    public static string? GetText(this SoraSegment segment)
        => segment.MessageType != SegmentType.Text ? null : ((TextSegment)segment.Data).Content;

    public static bool IsEmpty(this MessageBody msg)
        => msg.Count == 0 || (msg.Count == 1 && msg[0].Data is TextSegment { Content: "" });

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
            while (res[0].GetText() is { } text && string.IsNullOrWhiteSpace(text))
                res.RemoveAt(0);
            return (token, res);
        }
    }

    public static MessageBody Text(this MessageBody msg, string text)
    {
        msg.AddText(text);
        return msg;
    }

    public static MessageBody Image(this MessageBody msg, byte[] bytes)
    {
        using MemoryStream stream = new(bytes);
        msg.Add(SoraSegment.Image(stream));
        return msg;
    }

    public static ValueTask<(ApiStatus apiStatus, int messageId)> QuoteReply(
        this GroupMessageEventArgs eventArgs, MessageBody msg)
        => eventArgs.Reply(SoraSegment.Reply(eventArgs.Message.MessageId) + msg);
}
