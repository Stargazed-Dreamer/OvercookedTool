namespace OvercookedTool.Core.Models;

public sealed class SaveFileEntry
{
    public required string FileName { get; init; }
    public required string FullPath { get; init; }
    public long Size { get; init; }
    public DateTime LastWriteTime { get; init; }
    public int Slot { get; init; }
    public int? DlcId { get; init; }
    public bool IsMeta { get; init; }
    public int? StarCount { get; init; }
    public string Prefix { get; init; } = string.Empty;
    public string Group => DlcId.HasValue ? $"DLC{DlcId.Value}" : (string.IsNullOrWhiteSpace(Prefix) ? "CoopSlot" : Prefix);
}
