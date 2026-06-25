using System.Text.Json;

namespace OvercookedTool.App;

/// <summary>
/// 管理应用程序设置的存储类，提供加载、保存和操作设置的方法。
/// </summary>
internal static class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() // JSON序列化选项
    {
        WriteIndented = true, // 输出JSON时格式化缩进，便于阅读
    };

    private static readonly string SettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json"); // 设置文件路径，基于应用程序基础目录

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) // 检查设置文件是否存在
            {
                return new AppSettings(); // 文件不存在时返回默认设置
            }

            var json = File.ReadAllText(SettingsPath); // 读取JSON文件内容
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings(); // 反序列化JSON，若失败则使用默认设置
            settings.MaxRecentCount = Math.Clamp(settings.MaxRecentCount, 1, 100); // 约束最近项目数量在1到100之间
            settings.MaxBackupPerSave = Math.Clamp(settings.MaxBackupPerSave, 1, 50); // 约束每次保存的备份数量在1到50之间
            return settings;
        }
        catch
        {
            return new AppSettings(); // 捕获任何异常并返回默认设置
        }
    }

    public static void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions); // 序列化设置为JSON字符串
        File.WriteAllText(SettingsPath, json); // 将JSON字符串写入设置文件
    }

    public static void PushRecent(AppSettings settings, string path)
    {
        settings.RecentPackagePaths.RemoveAll(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase)); // 移除所有与指定路径相同的条目（不区分大小写）
        settings.RecentPackagePaths.Insert(0, path); // 将新路径插入列表开头
        while (settings.RecentPackagePaths.Count > Math.Max(1, settings.MaxRecentCount)) // 当列表大小超过最大数量（至少为1）时循环
        {
            settings.RecentPackagePaths.RemoveAt(settings.RecentPackagePaths.Count - 1); // 移除列表末尾的条目，以限制大小
        }
    }
}
