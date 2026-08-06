using System.Text.RegularExpressions;
using OvercookedTool.Core.Models;

namespace OvercookedTool.Core.Services;

/// <summary>
/// 用于解析和构建存档文件名的助手类
/// </summary>
public static partial class SaveFileNameHelper
{
    [GeneratedRegex(
        @"^(?:(?<prefix>(?!DLC\d+_)[A-Za-z0-9]+)_)?(?:DLC(?<dlc>\d+)_)?(?<kind>Meta|CoopSlot)_SaveFile_?(?<slot>\d*)\.(?<ext>save|json|sjson)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ModernPattern();

    [GeneratedRegex(
        @"^(?:(?:DLC(?<dlc>\d+))|(?<prefix>(?!DLC\d+$)[A-Za-z0-9]+))?(?<kind>CAMPAIGNSAVE)(?<slot>\d*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LegacyPattern();

    [GeneratedRegex(
        "^meta$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MetaPattern();

    /// <summary>
    /// 尝试解析文件名，提取存档条目信息
    /// </summary>
    /// <param name="fileName">要解析的文件名</param>
    /// <param name="fullPath">文件的完整路径</param>
    /// <param name="entry">输出的存档条目</param>
    /// <returns>是否成功解析</returns>
    public static bool TryParse(string fileName, string fullPath, out SaveFileEntry entry)
    {
        entry = null!;

        var fileInfo = new FileInfo(fullPath);
        // 尝试使用现代格式正则匹配
        var modern = ModernPattern().Match(fileName);
        if (modern.Success)
        {
            // 从匹配结果中提取存档类型
            var kind = modern.Groups["kind"].Value;
            // 判断是否为Meta存档
            var isMeta = kind.Equals("Meta", StringComparison.OrdinalIgnoreCase);
            var slotText = modern.Groups["slot"].Value;

            // 创建并填充SaveFileEntry对象
            entry = new SaveFileEntry
            {
                FileName = fileName,
                FullPath = fullPath,
                Size = fileInfo.Exists ? fileInfo.Length : 0,
                LastWriteTime = fileInfo.Exists ? fileInfo.LastWriteTime : DateTime.MinValue,
                // 尝试解析槽位，失败则默认为0
                Slot = int.TryParse(slotText, out var slot) ? slot : 0,
                // 尝试解析DLC ID，失败则为null
                DlcId = int.TryParse(modern.Groups["dlc"].Value, out var dlcId) ? dlcId : null,
                IsMeta = isMeta,
                StarCount = null,
                // 如果存在前缀则使用，否则为空字符串
                Prefix = modern.Groups["prefix"].Success ? modern.Groups["prefix"].Value : string.Empty,
            };
            return true;
        }

        // 尝试使用旧版格式正则匹配
        var legacy = LegacyPattern().Match(fileName);
        if (legacy.Success)
        {
            // 创建并填充SaveFileEntry对象
            entry = new SaveFileEntry
            {
                FileName = fileName,
                FullPath = fullPath,
                Size = fileInfo.Exists ? fileInfo.Length : 0,
                LastWriteTime = fileInfo.Exists ? fileInfo.LastWriteTime : DateTime.MinValue,
                // 尝试解析槽位，失败则默认为0
                Slot = int.TryParse(legacy.Groups["slot"].Value, out var slot) ? slot : 0,
                // 尝试解析DLC ID，失败则为null
                DlcId = int.TryParse(legacy.Groups["dlc"].Value, out var dlcId) ? dlcId : null,
                // 旧版格式始终不是Meta存档
                IsMeta = false,
                StarCount = null,
                // 如果存在前缀则使用，否则为空字符串
                Prefix = legacy.Groups["prefix"].Success ? legacy.Groups["prefix"].Value : string.Empty,
            };
            return true;
        }

        // 尝试匹配单独的"meta"文件名
        if (MetaPattern().IsMatch(fileName))
        {
            // 创建并填充SaveFileEntry对象
            entry = new SaveFileEntry
            {
                FileName = fileName,
                FullPath = fullPath,
                Size = fileInfo.Exists ? fileInfo.Length : 0,
                LastWriteTime = fileInfo.Exists ? fileInfo.LastWriteTime : DateTime.MinValue,
                // Meta文件没有槽位
                Slot = 0,
                // Meta文件没有DLC ID
                DlcId = null,
                // 这是Meta存档
                IsMeta = true,
                StarCount = null,
            };
            return true;
        }

        // 所有模式都不匹配，返回失败
        return false;
    }

    /// <summary>
    /// 根据平台和模板构建文件名
    /// </summary>
    /// <param name="platform">目标平台</param>
    /// <param name="template">作为模板的存档条目</param>
    /// <returns>构建的文件名</returns>
    public static string BuildFileName(SavePlatform platform, SaveFileEntry template)
    {
        // 如果是Meta存档，根据平台返回不同的Meta文件名
        if (template.IsMeta)
        {
            return platform switch
            {
                // AYCE JSON平台使用带扩展名的Meta文件名
                SavePlatform.AyceJson => "Meta_SaveFile.json",
                // Xbox二进制和Switch JSON平台使用简单的"meta"文件名
                SavePlatform.XboxBinary or SavePlatform.SwitchJson => "meta",
                // 其他平台（如默认二进制）使用.save扩展名的Meta文件名
                _ => "Meta_SaveFile.save",
            };
        }

        // 非Meta存档，根据平台构建不同的文件名格式
        return platform switch
        {
            // AYCE JSON平台使用现代格式，json扩展名
            SavePlatform.AyceJson => BuildModernName(template, "json"),
            // Xbox二进制和Switch JSON平台使用旧版格式
            SavePlatform.XboxBinary or SavePlatform.SwitchJson => BuildLegacyName(template),
            // 其他平台（如默认二进制）使用现代格式，save扩展名
            _ => BuildModernName(template, "save"),
        };
    }

    /// <summary>
    /// 创建一个新的存档条目副本，但使用指定的槽位
    /// </summary>
    /// <param name="source">源存档条目</param>
    /// <param name="slot">新的槽位</param>
    /// <returns>具有新槽位的存档条目</returns>
    public static SaveFileEntry WithSlot(SaveFileEntry source, int slot)
    {
        // 复制除槽位外的所有属性，槽位使用新值
        return new SaveFileEntry
        {
            FileName = source.FileName,
            FullPath = source.FullPath,
            Size = source.Size,
            LastWriteTime = source.LastWriteTime,
            Slot = slot,
            DlcId = source.DlcId,
            IsMeta = source.IsMeta,
            StarCount = source.StarCount,
            Prefix = source.Prefix,
        };
    }

    /// <summary>
    /// 构建现代格式的文件名
    /// </summary>
    /// <param name="template">存档条目模板</param>
    /// <param name="extension">文件扩展名</param>
    /// <returns>构建的文件名</returns>
    private static string BuildModernName(SaveFileEntry template, string extension)
    {
        // 构建组前缀：如果模板有前缀，则加上下划线分隔符
        var groupPrefix = !string.IsNullOrWhiteSpace(template.Prefix)
            ? template.Prefix + "_"
            : string.Empty;
        // 构建DLC前缀：如果有DLC ID，则加上DLC前缀
        var dlcPrefix = template.DlcId.HasValue ? $"DLC{template.DlcId.Value}_" : string.Empty;
        // 拼接最终文件名：组前缀 + DLC前缀 + 固定格式 + 槽位 + 扩展名
        return $"{groupPrefix}{dlcPrefix}CoopSlot_SaveFile_{template.Slot}.{extension}";
    }

    /// <summary>
    /// 构建旧版格式的文件名
    /// </summary>
    /// <param name="template">存档条目模板</param>
    /// <returns>构建的文件名</returns>
    private static string BuildLegacyName(SaveFileEntry template)
    {
        // 构建组前缀
        var groupPrefix = !string.IsNullOrWhiteSpace(template.Prefix)
            ? template.Prefix
            : string.Empty;
        // 旧版格式的DLC前缀：如果有DLC ID则使用DLC前缀，否则使用组前缀
        var dlcPrefix = template.DlcId.HasValue ? $"DLC{template.DlcId.Value}" : groupPrefix;
        // 拼接最终文件名：DLC/组前缀 + CAMPAIGNSAVE + 槽位
        return $"{dlcPrefix}CAMPAIGNSAVE{template.Slot}";
    }
}
