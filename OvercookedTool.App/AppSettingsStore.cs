using System.Text.Json;

namespace OvercookedTool.App;

internal static class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly string SettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            settings.MaxRecentCount = Math.Clamp(settings.MaxRecentCount, 1, 100);
            settings.MaxBackupPerSave = Math.Clamp(settings.MaxBackupPerSave, 1, 50);
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    public static void PushRecent(AppSettings settings, string path)
    {
        settings.RecentPackagePaths.RemoveAll(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase));
        settings.RecentPackagePaths.Insert(0, path);
        while (settings.RecentPackagePaths.Count > Math.Max(1, settings.MaxRecentCount))
        {
            settings.RecentPackagePaths.RemoveAt(settings.RecentPackagePaths.Count - 1);
        }
    }
}
