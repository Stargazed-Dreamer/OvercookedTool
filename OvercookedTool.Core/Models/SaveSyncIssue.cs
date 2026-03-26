namespace OvercookedTool.Core.Models;

public enum SaveSyncIssueType
{
    Conflict = 0,
    PendingSyncToBackup = 1,
    MissingBackup = 2,
}

public sealed class SaveSyncIssue
{
    public required SaveSyncIssueType Type { get; init; }
    public required SaveFileEntry Save { get; init; }
    public string? BackupPath { get; init; }
    public DateTime? BackupTime { get; init; }
    public required string Message { get; init; }
}
