using System.Reflection;
using TairitsuSora.Core;
using YukariToolBox.LightLog;

Log.LogConfiguration.EnableConsoleOutput().SetLogLevel(LogLevel.Info);
using Application app = Application.Instance;
app.RegisterCommandsInAssembly(Assembly.GetExecutingAssembly());
await app.RunAsync();
