namespace OvercookedTool.Core.Models;

/// <summary>
/// 用于保存包的上下文信息，包含包的路径、显示名称、平台、版本等属性。
/// </summary>
public sealed class SavePackageContext
{
    public required string PackagePath { get; init; }
    public required string DisplayName { get; init; }
    public SavePlatform Platform { get; init; }
    public SaveVersion Version { get; init; }
    public string? DetectedKey { get; init; }
    public string KeySource { get; init; } = "N/A";
    public bool KeyValidated { get; init; }
    public string? FriendCode { get; init; }
    public IReadOnlyList<SaveFileEntry> Saves { get; init; } = Array.Empty<SaveFileEntry>();
}

