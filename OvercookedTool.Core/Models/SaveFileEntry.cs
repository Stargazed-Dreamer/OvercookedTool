namespace OvercookedTool.Core.Models;

/// <summary>
/// 表示存档文件的条目信息，用于管理游戏或应用的存档数据。
/// </summary>
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
    public string Group => DlcId.HasValue ? $"DLC{DlcId.Value}" : (string.IsNullOrWhiteSpace(Prefix) ? "CoopSlot" : Prefix); // 根据DlcId和Prefix计算组名：若DlcId有值则组名为"DLC{DlcId}"，否则若Prefix非空则使用Prefix，否则默认为"CoopSlot"
}
