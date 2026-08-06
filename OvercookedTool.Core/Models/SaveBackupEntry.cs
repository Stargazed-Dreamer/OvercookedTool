namespace OvercookedTool.Core.Models;

/// <summary>
/// 表示一个保存备份的条目，包含备份路径、创建时间、大小和原因。
/// </summary>
public sealed class SaveBackupEntry
{
    public required string BackupPath { get; init; }
    public DateTime CreatedAt { get; init; }
    public long Size { get; init; }
    public string Reason { get; init; } = "unknown";
}
