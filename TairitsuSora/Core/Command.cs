using System.Text.Json.Nodes;
using TairitsuSora.Utils;

namespace TairitsuSora.Core;

public abstract class Command
{
    public const string TriggerPrefix = "/";
    public abstract CommandInfo Info { get; }
    public virtual ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public virtual ValueTask ExecuteAsync() => Application.Instance.CancellationToken.WaitUntilCanceled();

    public IReadOnlySet<long> EnabledGroups
    {
        get => _enabledGroups;
        set => _enabledGroups = new ConcurrentHashSet<long>(value);
    }

    public bool IsEnabledInGroup(long groupId) => _enabledGroups.Contains(groupId);

    public void ToggleGroupAvailability(long groupId, bool enabled)
    {
        if (enabled)
            _enabledGroups.Add(groupId);
        else
            _enabledGroups.Remove(groupId);
    }

    public virtual ValueTask<JsonNode?> CollectConfigAsync() => ValueTask.FromResult<JsonNode?>(null);
    public virtual ValueTask ApplyConfigAsync(JsonNode config) => ValueTask.CompletedTask;

    private volatile ConcurrentHashSet<long> _enabledGroups = [];
}

/// <summary>
/// Command information.
/// </summary>
/// <param name="Trigger">Command trigger. Set to null for non-triggerable commands.</param>
/// <param name="Togglable">Whether the command is togglable by admins.</param>
/// <param name="Listed">Whether the command is listed in the help message.</param>
/// <param name="DisplayName">Displayed name of the command, the class name will be used if it is null.</param>
/// <param name="Summary">Summary of a command's function.</param>
/// <param name="Description">Detailed description shown at the end of a full help message.</param>
public record CommandInfo(
    string? Trigger = null, bool Togglable = true, bool Listed = true,
    string? DisplayName = null, string Summary = "", string? Description = null
);
