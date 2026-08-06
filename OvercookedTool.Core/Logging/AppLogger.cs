using System.Globalization;
using System.Text;

namespace OvercookedTool.Core.Logging;

/// <summary>
/// AppLogger 是一个静态日志记录类，用于记录应用程序的日志信息。
/// 支持线程安全的操作、日志级别控制、文件写入和事件通知。
/// </summary>
public static class AppLogger
{
    // 用于线程同步的对象，确保多线程环境下的安全访问
    private static readonly object SyncRoot = new();
    // 日志文件的存储目录，默认为应用程序基础目录下的 "logs" 文件夹
    private static string _logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
    // 标记日志记录器是否已初始化
    private static bool _initialized;
    // 标记日志记录功能是否启用，默认为 true（启用）
    private static bool _enabled = true;
    // 日志保留天数，超过该天数的日志文件会在清理时被删除。0 表示永不清理。
    private static int _maxRetentionDays = 30;
    // 上一次执行日志清理的日期（按天节流，避免每次写入都扫描目录）
    private static DateTime _lastCleanupDate = DateTime.MinValue;

    // 当日志被写入时触发的事件，传递日志行字符串
    public static event Action<string>? LogEmitted;

    /// <summary>
    /// 日志文件名前缀，清理逻辑据此匹配本工具产生的日志文件。
    /// </summary>
    private const string LogFilePrefix = "overcookedtool-";

    /// <summary>
    /// 初始化日志记录器。
    /// </summary>
    /// <param name="logDirectory">可选的日志目录路径，如果为 null 或空则使用默认目录。</param>
    /// <param name="enabled">是否启用日志记录功能，默认为 true。</param>
    /// <param name="maxRetentionDays">日志保留天数，超过该天数的日志会被清理；默认 30 天，传 0 表示永不清理。</param>
    public static void Initialize(string? logDirectory = null, bool enabled = true, int maxRetentionDays = 30)
    {
        // 使用锁确保初始化过程的线程安全
        lock (SyncRoot)
        {
            // 如果已初始化，则直接返回，避免重复初始化
            if (_initialized)
            {
                return;
            }

            // 设置日志启用状态
            _enabled = enabled;
            // 规范化保留天数：负数视为 0（永不清理）
            _maxRetentionDays = Math.Max(0, maxRetentionDays);
            // 如果提供了有效的日志目录，则更新 _logDirectory 字段
            if (!string.IsNullOrWhiteSpace(logDirectory))
            {
                _logDirectory = logDirectory;
            }

            // 标记为已初始化
            _initialized = true;
            // 如果日志功能启用，则创建日志目录、清理过期日志并记录初始化信息
            if (_enabled)
            {
                Directory.CreateDirectory(_logDirectory);
                PurgeExpiredLogs(DateTime.Today);
                Info("Logger initialized.");
            }
        }
    }

    /// <summary>
    /// 设置日志记录功能的启用状态。
    /// </summary>
    /// <param name="enabled">是否启用日志记录。</param>
    public static void SetEnabled(bool enabled)
    {
        // 使用锁确保设置操作的线程安全
        lock (SyncRoot)
        {
            // 更新启用状态
            _enabled = enabled;
            // 如果启用日志，则创建目录并记录启用信息
            if (_enabled)
            {
                Directory.CreateDirectory(_logDirectory);
                Write("INFO", "Logging enabled.");
            }
        }
    }

    /// <summary>
    /// 设置日志保留天数。超过该天数的日志文件会在下一次清理周期被删除。
    /// </summary>
    /// <param name="maxRetentionDays">保留天数，0 表示永不清理。</param>
    public static void SetRetention(int maxRetentionDays)
    {
        lock (SyncRoot)
        {
            _maxRetentionDays = Math.Max(0, maxRetentionDays);
        }
    }

    /// <summary>
    /// 记录 INFO 级别的日志。
    /// </summary>
    /// <param name="message">日志消息。</param>
    public static void Info(string message) => Write("INFO", message);

    /// <summary>
    /// 记录 WARN 级别的日志。
    /// </summary>
    /// <param name="message">日志消息。</param>
    public static void Warn(string message) => Write("WARN", message);

    /// <summary>
    /// 记录 ERROR 级别的日志，可选地附加异常信息。
    /// </summary>
    /// <param name="message">日志消息。</param>
    /// <param name="ex">可选的异常对象，如果提供则附加到消息中。</param>
    public static void Error(string message, Exception? ex = null)
    {
        // 如果有异常，则将异常信息附加到消息中；否则只使用消息本身
        var detail = ex is null ? message : $"{message}{Environment.NewLine}{ex}";
        Write("ERROR", detail);
    }

    /// <summary>
    /// 写入日志到文件的核心方法。
    /// </summary>
    /// <param name="level">日志级别（如 INFO、WARN、ERROR）。</param>
    /// <param name="message">日志消息内容。</param>
    private static void Write(string level, string message)
    {
        // 使用锁确保写入操作的线程安全
        lock (SyncRoot)
        {
            // 如果未初始化，则强制标记为已初始化（用于在 Initialize 之前调用的情况）
            if (!_initialized)
            {
                _initialized = true;
            }

            // 如果日志功能未启用，则直接返回
            if (!_enabled)
            {
                return;
            }

            // 确保日志目录存在
            Directory.CreateDirectory(_logDirectory);
            // 格式化日志行：包含时间戳、级别和消息
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
            // 生成当日的日志文件路径，文件名基于日期
            var filePath = Path.Combine(_logDirectory, $"{LogFilePrefix}{DateTime.Now:yyyyMMdd}.log");
            // 将日志行追加到文件中，使用 UTF-8 编码
            File.AppendAllText(filePath, line + Environment.NewLine, Encoding.UTF8);
            // 触发 LogEmitted 事件，通知订阅者
            LogEmitted?.Invoke(line);
            // 按天节流清理过期日志（同一天内只执行一次）
            TryPurgeExpiredLogs();
        }
    }

    /// <summary>
    /// 按天节流触发过期日志清理。同一天内只执行一次实际清理。
    /// 必须在 SyncRoot 锁内调用。
    /// </summary>
    private static void TryPurgeExpiredLogs()
    {
        var today = DateTime.Today;
        if (today == _lastCleanupDate)
        {
            return;
        }

        _lastCleanupDate = today;
        PurgeExpiredLogs(today);
    }

    /// <summary>
    /// 删除超过保留天数的日志文件。
    /// 日志文件名形如 overcookedtool-yyyyMMdd.log，按文件名解析日期；无法解析的文件保留不动。
    /// </summary>
    /// <param name="today">当前日期，用于计算截止时间。</param>
    private static void PurgeExpiredLogs(DateTime today)
    {
        if (_maxRetentionDays <= 0)
        {
            return;
        }

        if (!Directory.Exists(_logDirectory))
        {
            return;
        }

        var cutoff = today.AddDays(-_maxRetentionDays);
        string[] files;
        try
        {
            files = Directory.GetFiles(_logDirectory, $"{LogFilePrefix}*.log", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return;
        }

        foreach (var path in files)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            // 去掉前缀得到纯日期部分 yyyyMMdd
            if (name.Length <= LogFilePrefix.Length)
            {
                continue;
            }

            var datePart = name[LogFilePrefix.Length..];
            if (!DateTime.TryParseExact(
                    datePart,
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var fileDate))
            {
                // 文件名无法解析为日期，跳过不删（避免误删非本工具日志）
                continue;
            }

            if (fileDate < cutoff)
            {
                try
                {
                    File.Delete(path);
                }
                catch
                {
                    // 清理失败不影响主流程（文件可能被占用）
                }
            }
        }
    }
}
