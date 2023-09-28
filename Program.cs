using TairitsuSora.Core;
using YukariToolBox.LightLog;

Log.LogConfiguration.EnableConsoleOutput().SetLogLevel(LogLevel.Info);
await Application.Instance.RunAsync();
