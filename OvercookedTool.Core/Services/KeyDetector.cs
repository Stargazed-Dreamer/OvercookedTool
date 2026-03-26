using System.Text.RegularExpressions;
using OvercookedTool.Core.Crypto;
using OvercookedTool.Core.Models;

namespace OvercookedTool.Core.Services;

public sealed class KeyDetector
{
    private const string EpicFallbackKey = "Epic.OnlineServices.EpicAccountId";

    public (bool Success, string? Key, string Source) DetectKey(
        string packagePath,
        SavePlatform platform,
        IReadOnlyList<SaveFileEntry> saves,
        string? preferredKey)
    {
        if (platform is SavePlatform.AyceJson or SavePlatform.SwitchJson)
        {
            return (true, null, "JSON存档无需密钥");
        }

        var candidates = BuildCandidates(packagePath, preferredKey);
        var probe = saves.FirstOrDefault(x => !x.IsMeta) ?? saves.FirstOrDefault();
        if (probe is null)
        {
            var fallback = candidates.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(fallback.Key))
            {
                return (false, fallback.Key, $"{fallback.Source}(未验证)");
            }

            return (false, null, "未找到可用于探测的存档文件");
        }

        var bytes = File.ReadAllBytes(probe.FullPath);

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.Key))
            {
                continue;
            }

            if (OvercookedCrypto.TryDecryptToJsonText(bytes, candidate.Key, out _))
            {
                return (true, candidate.Key, candidate.Source);
            }
        }

        return (false, null, "自动密钥探测失败");
    }

    public string? TryExtractFriendCode(string packagePath)
    {
        var vdfPath = Path.Combine(packagePath, "steam_autocloud.vdf");
        if (!File.Exists(vdfPath))
        {
            return null;
        }

        var content = File.ReadAllText(vdfPath);
        var match = Regex.Match(content, "\"accountid\"\\s*\"(?<id>\\d+)\"", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups["id"].Value;
        }

        match = Regex.Match(content, "\"(?<id>\\d{6,})\"");
        return match.Success ? match.Groups["id"].Value : null;
    }

    private static IReadOnlyList<(string Key, string Source)> BuildCandidates(string packagePath, string? preferredKey)
    {
        var list = new List<(string Key, string Source)>();
        void Add(string? key, string source)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (list.Any(x => string.Equals(x.Key, key, StringComparison.Ordinal)))
            {
                return;
            }

            list.Add((key.Trim(), source));
        }

        Add(preferredKey, "手动输入密钥");

        var folderName = Path.GetFileName(Path.GetFullPath(packagePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        Add(folderName, "目录名");

        var steamVdf = Path.Combine(packagePath, "steam_autocloud.vdf");
        if (File.Exists(steamVdf))
        {
            var content = File.ReadAllText(steamVdf);
            var match = Regex.Match(content, "\"accountid\"\\s*\"(?<id>\\d+)\"", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var accountIdText = match.Groups["id"].Value;
                Add(accountIdText, "steam_autocloud.vdf/accountid");
                if (ulong.TryParse(accountIdText, out var accountId))
                {
                    const ulong steamBase = 76561197960265728UL;
                    Add((steamBase + accountId).ToString(), "steam_autocloud.vdf/steamid64");
                }
            }
        }

        var directoryInfo = new DirectoryInfo(packagePath);
        for (var i = 0; i < 4 && directoryInfo is not null; i++, directoryInfo = directoryInfo.Parent)
        {
            var name = directoryInfo.Name;
            if (Regex.IsMatch(name, "^\\d{6,}$"))
            {
                Add(name, "父目录数字ID");
            }
        }

        Add(EpicFallbackKey, "Epic常见密钥");
        return list;
    }
}
