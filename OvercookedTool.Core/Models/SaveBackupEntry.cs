namespace OvercookedTool.Core.Models;

public sealed class SaveBackupEntry
{
    public required string BackupPath { get; init; }
    public DateTime CreatedAt { get; init; }
    public long Size { get; init; }
    public string Reason { get; init; } = "unknown";
}
