﻿namespace OvercookedTool.App;

/// <summary>
/// 应用程序设置类，用于存储应用配置参数。
/// </summary>
internal sealed class AppSettings
{
    // 最近使用的包路径列表
    public List<string> RecentPackagePaths { get; set; } = new();
    // 启用导入时自动检测
    public bool EnableAutoDetectOnImport { get; set; } = true;
    // 最大最近项目数
    public int MaxRecentCount { get; set; } = 20;
    // 启用日志记录
    public bool EnableLogging { get; set; } = true;
    // 每次保存时的最大备份数
    public int MaxBackupPerSave { get; set; } = 10;
    // Unity 设备标识符，可为 null
    public string? UnityDeviceId { get; set; }
}
