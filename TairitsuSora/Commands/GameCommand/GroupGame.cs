using System.Collections.Concurrent;
using Sora.EventArgs.SoraEvent;
using TairitsuSora.Core;
using TairitsuSora.Utils;

namespace TairitsuSora.TairitsuSora.Commands.GameCommand;

public abstract class GroupGame : Command
{
    protected async ValueTask StartGame(GroupMessageEventArgs ev,
        Func<GroupMessageEventArgs, CancellationToken, ValueTask> gameProcedure)
    {
        GameState state = new();
        if (!_activeGames.TryAdd(ev.SourceGroup.Id, state))
        {
            await ev.QuoteReply("你先别急");
            return;
        }
        var token = state.CancelToken;
        try { await gameProcedure(ev, token); }
        finally { _activeGames.Remove(ev.SourceGroup.Id, out _); }
    }

    protected async ValueTask<bool> CancelGame(GroupMessageEventArgs ev)
    {
        if (_activeGames.TryGetValue(ev.SourceGroup.Id, out var state))
        {
            state.Cancel();
            return true;
        }
        await ev.QuoteReply("当前群并没有正在进行的游戏");
        return false;
    }

    private class GameState
    {
        public CancellationToken CancelToken => _src.Token;
        public void Cancel() => _src.Cancel();
        private CancellationTokenSource _src =
            CancellationTokenSource.CreateLinkedTokenSource(Application.Instance.CancellationToken);
    }

    private ConcurrentDictionary<long, GameState> _activeGames = new();
}
