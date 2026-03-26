using OvercookedTool.Core.Logging;

namespace OvercookedTool.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var settings = AppSettingsStore.Load();
        var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        AppLogger.Initialize(logDir, settings.EnableLogging);

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                AppLogger.Error("Unhandled AppDomain exception.", ex);
            }
        };

        Application.ThreadException += (_, args) =>
        {
            AppLogger.Error("Unhandled UI thread exception.", args.Exception);
            MessageBox.Show(args.Exception.Message, "发生未处理异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLogger.Error("Unobserved task exception.", args.Exception);
            args.SetObserved();
        };

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(settings));
    }
}
