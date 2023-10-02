using Sora.Entities;
using Sora.Entities.Info;
using Sora.Entities.Segment.DataModel;
using Sora.Enumeration.EventParamsType;
using Sora.EventArgs.SoraEvent;
using TairitsuSora.Core;
using TairitsuSora.Utils;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class Admin : Command
{
    public override CommandInfo Info => new()
    {
        Trigger = "admin",
        Summary = "管理员指令"
    };

    [MessageHandler(Signature = "mute $at $timeSpan", Description = "禁言 [at] 时长为 [timeSpan]")]
    public async ValueTask MuteMember(GroupMessageEventArgs ev, AtSegment at, TimeSpan timeSpan)
    {
        if (await CheckMutability(ev, at) is not
            { muteTarget: var target, isPunishment: var isPunishment })
            return;
        TimeSpan min = TimeSpan.FromMinutes(isPunishment ? 5 : 1);
        TimeSpan max = TimeSpan.FromDays(30) - TimeSpan.FromSeconds(1);
        if (timeSpan < min) timeSpan = min;
        if (timeSpan > max) timeSpan = max;
        await ev.SourceGroup.EnableGroupMemberMute(target, (long)timeSpan.TotalSeconds);
        if (isPunishment) await ev.QuoteReply("劝你谨言慎行");
    }

    [MessageHandler(Signature = "unmute $at", Description = "解除禁言 [at]")]
    public async ValueTask UnmuteMember(GroupMessageEventArgs ev, AtSegment at)
    {
        if (await CheckMutability(ev, at) is not
            { muteTarget: var target, isPunishment: var isPunishment })
            return;
        if (isPunishment)
        {
            await ev.SourceGroup.EnableGroupMemberMute(target, 300); // 5min
            await ev.QuoteReply("你也冷静一下");
            return;
        }
        await ev.SourceGroup.DisableGroupMemberMute(target);
    }

    private MemberRoleType GetRole(GroupSenderInfo sender)
    {
        MemberRoleType role = sender.Role;
        if (Application.Instance.Admins.Contains(sender.UserId))
            role = MemberRoleType.Owner;
        return role;
    }

    private (long? muteTarget, bool isPunishment) FindMuteTarget(
        GroupMemberInfo self, GroupMemberInfo target, GroupSenderInfo issuer)
    {
        if (self.Role < MemberRoleType.Admin) return (null, false);
        var issuerRole = GetRole(issuer);
        if (issuer.UserId == target.UserId) // Use issuerRole here for super admin detection
            return (self.Role > issuerRole ? issuer.UserId : null, false);
        if (issuerRole > target.Role)
            return (self.Role > target.Role ? target.UserId : null, false);
        return (self.Role > issuerRole ? issuer.UserId : null, true);
    }

    private async ValueTask<(long muteTarget, bool isPunishment)?> CheckMutability(
        GroupMessageEventArgs ev, AtSegment at)
    {
        (ApiStatus _, GroupMemberInfo selfInfo) =
            await ev.SourceGroup.GetGroupMemberInfo(Application.Instance.SelfId);
        long target = long.Parse(at.Target);
        (ApiStatus _, GroupMemberInfo targetInfo) =
            await ev.SourceGroup.GetGroupMemberInfo(target);
        var (mutedTarget, isPunishment) = FindMuteTarget(selfInfo, targetInfo, ev.SenderInfo);
        if (mutedTarget is { } id) return (id, isPunishment);
        await ev.QuoteReply("这我做不到啊");
        return null;
    }
}
