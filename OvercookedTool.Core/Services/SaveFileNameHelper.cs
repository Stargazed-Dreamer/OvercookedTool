using System.Text.RegularExpressions;
using OvercookedTool.Core.Models;

namespace OvercookedTool.Core.Services;

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

    public static bool TryParse(string fileName, string fullPath, out SaveFileEntry entry)
    {
        entry = null!;

        var fileInfo = new FileInfo(fullPath);
        var modern = ModernPattern().Match(fileName);
        if (modern.Success)
        {
            var kind = modern.Groups["kind"].Value;
            var isMeta = kind.Equals("Meta", StringComparison.OrdinalIgnoreCase);
            var slotText = modern.Groups["slot"].Value;

            entry = new SaveFileEntry
            {
                FileName = fileName,
                FullPath = fullPath,
                Size = fileInfo.Exists ? fileInfo.Length : 0,
                LastWriteTime = fileInfo.Exists ? fileInfo.LastWriteTime : DateTime.MinValue,
                Slot = int.TryParse(slotText, out var slot) ? slot : 0,
                DlcId = int.TryParse(modern.Groups["dlc"].Value, out var dlcId) ? dlcId : null,
                IsMeta = isMeta,
                StarCount = null,
                Prefix = modern.Groups["prefix"].Success ? modern.Groups["prefix"].Value : string.Empty,
            };
            return true;
        }

        var legacy = LegacyPattern().Match(fileName);
        if (legacy.Success)
        {
            entry = new SaveFileEntry
            {
                FileName = fileName,
                FullPath = fullPath,
                Size = fileInfo.Exists ? fileInfo.Length : 0,
                LastWriteTime = fileInfo.Exists ? fileInfo.LastWriteTime : DateTime.MinValue,
                Slot = int.TryParse(legacy.Groups["slot"].Value, out var slot) ? slot : 0,
                DlcId = int.TryParse(legacy.Groups["dlc"].Value, out var dlcId) ? dlcId : null,
                IsMeta = false,
                StarCount = null,
                Prefix = legacy.Groups["prefix"].Success ? legacy.Groups["prefix"].Value : string.Empty,
            };
            return true;
        }

        if (MetaPattern().IsMatch(fileName))
        {
            entry = new SaveFileEntry
            {
                FileName = fileName,
                FullPath = fullPath,
                Size = fileInfo.Exists ? fileInfo.Length : 0,
                LastWriteTime = fileInfo.Exists ? fileInfo.LastWriteTime : DateTime.MinValue,
                Slot = 0,
                DlcId = null,
                IsMeta = true,
                StarCount = null,
            };
            return true;
        }

        return false;
    }

    public static string BuildFileName(SavePlatform platform, SaveFileEntry template)
    {
        if (template.IsMeta)
        {
            return platform switch
            {
                SavePlatform.AyceJson => "Meta_SaveFile.json",
                SavePlatform.XboxBinary or SavePlatform.SwitchJson => "meta",
                _ => "Meta_SaveFile.save",
            };
        }

        return platform switch
        {
            SavePlatform.AyceJson => BuildModernName(template, "json"),
            SavePlatform.XboxBinary or SavePlatform.SwitchJson => BuildLegacyName(template),
            _ => BuildModernName(template, "save"),
        };
    }

    public static SaveFileEntry WithSlot(SaveFileEntry source, int slot)
    {
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

    private static string BuildModernName(SaveFileEntry template, string extension)
    {
        var groupPrefix = !string.IsNullOrWhiteSpace(template.Prefix)
            ? template.Prefix + "_"
            : string.Empty;
        var dlcPrefix = template.DlcId.HasValue ? $"DLC{template.DlcId.Value}_" : string.Empty;
        return $"{groupPrefix}{dlcPrefix}CoopSlot_SaveFile_{template.Slot}.{extension}";
    }

    private static string BuildLegacyName(SaveFileEntry template)
    {
        var groupPrefix = !string.IsNullOrWhiteSpace(template.Prefix)
            ? template.Prefix
            : string.Empty;
        var dlcPrefix = template.DlcId.HasValue ? $"DLC{template.DlcId.Value}" : groupPrefix;
        return $"{dlcPrefix}CAMPAIGNSAVE{template.Slot}";
    }
}
