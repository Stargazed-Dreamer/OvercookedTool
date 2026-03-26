using System.Text;

namespace OvercookedTool.Core.Logging;

public static class AppLogger
{
    private static readonly object SyncRoot = new();
    private static string _logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
    private static bool _initialized;
    private static bool _enabled = true;

    public static event Action<string>? LogEmitted;

    public static void Initialize(string? logDirectory = null, bool enabled = true)
    {
        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            _enabled = enabled;
            if (!string.IsNullOrWhiteSpace(logDirectory))
            {
                _logDirectory = logDirectory;
            }

            _initialized = true;
            if (_enabled)
            {
                Directory.CreateDirectory(_logDirectory);
                Info("Logger initialized.");
            }
        }
    }

    public static void SetEnabled(bool enabled)
    {
        lock (SyncRoot)
        {
            _enabled = enabled;
            if (_enabled)
            {
                Directory.CreateDirectory(_logDirectory);
                Write("INFO", "Logging enabled.");
            }
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null)
    {
        var detail = ex is null ? message : $"{message}{Environment.NewLine}{ex}";
        Write("ERROR", detail);
    }

    private static void Write(string level, string message)
    {
        lock (SyncRoot)
        {
            if (!_initialized)
            {
                _initialized = true;
            }

            if (!_enabled)
            {
                return;
            }

            Directory.CreateDirectory(_logDirectory);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
            var filePath = Path.Combine(_logDirectory, $"overcookedtool-{DateTime.Now:yyyyMMdd}.log");
            File.AppendAllText(filePath, line + Environment.NewLine, Encoding.UTF8);
            LogEmitted?.Invoke(line);
        }
    }
}
