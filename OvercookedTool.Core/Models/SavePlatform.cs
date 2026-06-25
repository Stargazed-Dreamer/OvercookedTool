namespace OvercookedTool.Core.Models;

/// <summary>
/// 枚举类型，用于标识不同的保存平台。
/// </summary>
public enum SavePlatform
{
    Unknown = 0, // 未知平台
    Oc2Binary = 1, // OC2二进制格式
    AyceJson = 2, // Ayce JSON格式
    XboxBinary = 3, // Xbox二进制格式
    SwitchJson = 4, // Switch JSON格式
}
