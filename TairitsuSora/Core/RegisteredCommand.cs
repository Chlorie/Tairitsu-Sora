using YukariToolBox.LightLog;

namespace TairitsuSora.Core;

public class RegisteredCommand
{
    public Command Command { get; }
    public string Name { get; }
    public CommandInfo Info { get; }

    public async ValueTask InitializeAsync()
    {
        await Command.InitializeAsync();
        Log.Info(Application.AppName, $"Initialized command {Name}");
    }

    public RegisteredCommand(Command cmd)
    {
        Command = cmd;
        Name = cmd.GetType().FullName!;
        Info = cmd.Info;
    }
}
