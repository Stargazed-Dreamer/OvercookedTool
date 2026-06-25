namespace OvercookedTool.Core.Models;

/// <summary>
/// 表示保存同步问题的类型枚举。
/// 用于识别和分类数据同步过程中可能出现的不同状况。
/// </summary>
public enum SaveSyncIssueType
{
    /// <summary>
    /// 数据存在冲突。本地与备份数据不一致，需要解决。
    /// </summary>
    Conflict = 0,
    /// <summary>
    /// 等待同步到备份。数据已修改，但尚未成功备份。
    /// </summary>
    PendingSyncToBackup = 1,
    /// <summary>
    /// 备份缺失。无法找到对应的备份数据。
    /// </summary>
    MissingBackup = 2,
}

/// <summary>
/// 表示保存同步问题的数据结构，包含问题类型、相关保存条目、备份信息和错误消息。
/// </summary>
public sealed class SaveSyncIssue
{
    public required SaveSyncIssueType Type { get; init; }
    public required SaveFileEntry Save { get; init; }
    public string? BackupPath { get; init; }
    public DateTime? BackupTime { get; init; }
    public required string Message { get; init; }
}
