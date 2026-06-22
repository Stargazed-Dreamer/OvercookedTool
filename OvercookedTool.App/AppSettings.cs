namespace OvercookedTool.App;

internal sealed class AppSettings
{
    public List<string> RecentPackagePaths { get; set; } = new();
    public bool EnableAutoDetectOnImport { get; set; } = true;
    public int MaxRecentCount { get; set; } = 20;
    public bool EnableLogging { get; set; } = true;
    public int MaxBackupPerSave { get; set; } = 10;
    public string? UnityDeviceId { get; set; }
}
