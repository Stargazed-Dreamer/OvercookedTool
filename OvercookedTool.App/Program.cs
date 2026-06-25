using OvercookedTool.Core.Logging;

namespace OvercookedTool.App;

/// <summary>
/// 应用程序的主程序类，包含入口点Main方法，用于初始化配置和处理未处理异常。
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // 加载应用程序设置
        var settings = AppSettingsStore.Load();
        // 设置日志目录路径，基于应用程序基目录
        var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        // 初始化日志记录器，根据设置启用或禁用日志
        AppLogger.Initialize(logDir, settings.EnableLogging);

        // 注册AppDomain未处理异常事件处理器，用于捕获全局未处理异常
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            // 检查异常对象是否为Exception类型，如果是则记录错误日志
            if (args.ExceptionObject is Exception ex)
            {
                AppLogger.Error("Unhandled AppDomain exception.", ex);
            }
        };

        // 注册UI线程异常事件处理器，用于捕获Windows窗体应用程序中的未处理异常
        Application.ThreadException += (_, args) =>
        {
            // 记录UI线程异常错误日志
            AppLogger.Error("Unhandled UI thread exception.", args.Exception);
            // 显示错误消息框，向用户报告异常
            MessageBox.Show(args.Exception.Message, "发生未处理异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        // 注册任务调度器未观察异常事件处理器，用于捕获异步任务中未被观察的异常
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            // 记录未观察任务异常错误日志
            AppLogger.Error("Unobserved task exception.", args.Exception);
            // 标记异常为已观察，防止其传播到垃圾回收器
            args.SetObserved();
        };

        // 初始化应用程序配置
        ApplicationConfiguration.Initialize();
        // 创建并运行主窗体，启动应用程序
        Application.Run(new MainForm(settings));
    }
}
