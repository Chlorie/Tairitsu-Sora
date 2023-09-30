using System.Reflection;
using System.Text.Json.Nodes;
using Sora;
using Sora.Entities.Base;
using Sora.Interfaces;
using Sora.Net.Config;
using TairitsuSora.Utils;
using YukariToolBox.LightLog;

namespace TairitsuSora.Core;

public class Application
{
    public const string AppName = "Tairitsu";

    public static Application Instance => _instance ?? new Application();
    public static ISoraService Service => Instance._service;
    public static SoraApi Api => Instance._api!;

    public long SelfId => _config.BotId;
    public IReadOnlyList<long> Admins => _config.Admins;
    public CancellationToken CancellationToken => _cancelSrc.Token;
    public IReadOnlyCollection<RegisteredCommand> Commands => _cmds.Values;

    public void RegisterCommand(Command cmd)
    {
        string cmdName = cmd.GetType().FullName!;
        if (_cmds.ContainsKey(cmdName))
        {
            Log.Warning(AppName,
                $"Command class {cmdName} is already registered, ignoring this instance");
            return;
        }
        _cmds[cmdName] = new RegisteredCommand(cmd);
    }

    public void RegisterCommand<TCommand>() where TCommand : Command, new() => RegisterCommand(new TCommand());

    public void RegisterCommandsInAssembly(Assembly assembly)
    {
        foreach (Type type in assembly.GetTypes())
        {
            if (type.GetCustomAttribute<RegisterCommandAttribute>() is null) continue;
            if (!type.IsSubclassOf(typeof(Command)))
                throw new ArgumentException(
                    $"A class with {nameof(RegisterCommandAttribute)} is not derived from {nameof(Command)}");
            if (type.GetConstructor(Array.Empty<Type>()) is not { } ctorInfo)
                throw new ArgumentException(
                    $"A command class marked with {nameof(RegisterCommandAttribute)} is not " +
                    $"default constructible, thus it is skipped");
            RegisterCommand((Command)ctorInfo.Invoke(null));
        }
    }

    public async ValueTask RunAsync()
    {
        _service.ConnManager.OnOpenConnectionAsync += (id, _) =>
        {
            _api = _service.GetApi(id);
            return ValueTask.CompletedTask;
        };
        await _service.StartService();
        await InitializeCommands();
        List<ValueTask> tasks = new() { WaitForStopAsync(), SaveConfigAsync() };
        // TODO: also await message commands
        tasks.AddRange(_cmds.Values.Select(
            static cmd => cmd.ExecuteAsync().IgnoreCancellation()));
        await tasks.WhenAll();
        Log.Info(AppName, "Application stopped");
        await _service.StopService();
    }

    private const string ConfigPath = "data/config.json";
    private const string StopCommand = "/stop";
    private static Application? _instance;

    private BotConfig _config;
    private ISoraService _service;
    private volatile SoraApi? _api;
    private CancellationTokenSource _cancelSrc = new();
    private Dictionary<string, RegisteredCommand> _cmds = new();

    private Application()
    {
        if (_instance is not null)
            throw new InvalidOperationException("There should not be more than one application instance");
        _instance = this;
        _config = BotConfig.Load(ConfigPath);
        Log.Info(AppName, $"Loaded config from {ConfigPath}");
        _service = SoraServiceFactory.CreateService(new ClientConfig
        {
            AccessToken = _config.OneBotConfig.AccessToken,
            Host = _config.OneBotConfig.Host,
            Port = _config.OneBotConfig.Port
        });
    }

    private async ValueTask InitializeCommands()
    {
        // Apply enabled groups config
        foreach (var (cmdName, groups) in _config.CommandEnabledGroups)
            if (_cmds.TryGetValue(cmdName, out var cmd))
                cmd.Command.EnabledGroups = groups;
            else
                WarnExtraCommandInConfig(cmdName);

        // Apply command-specific config
        List<ValueTask> applyConfigTasks = new();
        foreach (var (cmdName, config) in _config.CommandConfigs)
            if (_cmds.TryGetValue(cmdName, out var cmd))
                applyConfigTasks.Add(cmd.Command.ApplyConfigAsync(config).IgnoreException());
            else
                WarnExtraCommandInConfig(cmdName);
        await applyConfigTasks.WhenAll();

        // Initialize commands
        static async ValueTask<string?> InitializeCommand(RegisteredCommand cmd)
        {
            try
            {
                await cmd.InitializeAsync();
                return null;
            }
            catch (Exception e)
            {
                Log.Error(e, AppName, $"Failed to initialize command {cmd.Name}");
                return cmd.Name;
            }
        }

        var failedCmds = await _cmds.Values.Select(InitializeCommand).WhenAll();
        foreach (var cmdName in failedCmds)
            if (cmdName is not null)
                _cmds.Remove(cmdName);
    }

    private async ValueTask WaitForStopAsync()
    {
        void Body()
        {
            while (Console.ReadLine() != StopCommand) { }
            Log.Info(AppName, "Received stop signal, trying to stop all commands");
            _cancelSrc.Cancel();
        }

        // Use Task.Run to move the blocking ReadLine() onto another thread
        await Task.Run(Body, CancellationToken);
    }

    private async ValueTask SaveConfigAsync()
    {
        async ValueTask LoopBody()
        {
            await Task.Delay(TimeSpan.FromMinutes(30), CancellationToken);
            await SyncAndSaveConfig();
        }

        static async ValueTask<(string name, JsonNode? config)>
            GetCommandConfig(KeyValuePair<string, RegisteredCommand> kv)
        {
            var (cmdName, cmd) = kv;
            return (cmdName, await cmd.Command.CollectConfigAsync());
        }

        async ValueTask SyncAndSaveConfig()
        {
            foreach (var (cmdName, cmd) in _cmds)
                if (cmd.Info.Togglable)
                    _config.CommandEnabledGroups[cmdName] = new HashSet<long>(cmd.Command.EnabledGroups);
            var configs = await _cmds.Select(GetCommandConfig).WhenAll();
            foreach (var (cmdName, config) in configs)
            {
                if (config is null)
                    _config.CommandConfigs.Remove(cmdName);
                else
                    _config.CommandConfigs[cmdName] = config;
            }
            _config.Save(ConfigPath);
            Log.Info(AppName, $"Saved config to {ConfigPath}");
        }

        await AsyncExtensions.LoopUntilCancellation(LoopBody);
        await SyncAndSaveConfig();
    }

    private void WarnExtraCommandInConfig(string cmdName) => Log.Warning(AppName,
        $"Found config for command type {cmdName}, but such a type isn't registered in the application");
}
