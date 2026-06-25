using System.Text.RegularExpressions;
using OvercookedTool.Core.Crypto;
using OvercookedTool.Core.Models;

namespace OvercookedTool.Core.Services;

/// <summary>
/// 密钥检测器类，用于自动探测和提取存档文件的解密密钥。
/// </summary>
public sealed class KeyDetector
{
    private const string EpicFallbackKey = "Epic.OnlineServices.EpicAccountId";

    /// <summary>
    /// 探测存档包的解密密钥。
    /// </summary>
    /// <param name="packagePath">存档包路径。</param>
    /// <param name="platform">存档平台类型。</param>
    /// <param name="saves">存档文件列表。</param>
    /// <param name="preferredKey">用户手动指定的首选密钥。</param>
    /// <param name="unityDeviceId">Unity设备标识（可选）。</param>
    /// <returns>包含探测状态、密钥和来源信息的元组。</returns>
    public (bool Success, string? Key, string Source) DetectKey(
        string packagePath,
        SavePlatform platform,
        IReadOnlyList<SaveFileEntry> saves,
        string? preferredKey,
        string? unityDeviceId = null)
    {
        // JSON格式存档无需密钥，直接返回成功状态
        if (platform is SavePlatform.AyceJson or SavePlatform.SwitchJson)
        {
            return (true, null, "JSON存档无需密钥");
        }

        // 构建候选密钥列表并选择第一个非元数据存档文件用于探测
        var candidates = BuildCandidates(packagePath, preferredKey, unityDeviceId);
        var probe = saves.FirstOrDefault(x => !x.IsMeta) ?? saves.FirstOrDefault();
        
        // 若无存档文件可用，则使用候选列表中的第一个密钥作为未验证的回退方案
        if (probe is null)
        {
            var fallback = candidates.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(fallback.Key))
            {
                return (false, fallback.Key, $"{fallback.Source}(未验证)");
            }

            return (false, null, "未找到可用于探测的存档文件");
        }

        // 读取探测存档文件的原始字节数据
        var bytes = File.ReadAllBytes(probe.FullPath);

        // 依次尝试使用候选密钥解密存档，成功则返回
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

    /// <summary>
    /// 尝试从Steam云配置文件中提取好友代码（账户ID）。
    /// </summary>
    /// <param name="packagePath">存档包路径。</param>
    /// <returns>提取到的好友代码，若未找到则返回null。</returns>
    public string? TryExtractFriendCode(string packagePath)
    {
        // 定位并检查Steam云配置文件是否存在
        var vdfPath = Path.Combine(packagePath, "steam_autocloud.vdf");
        if (!File.Exists(vdfPath))
        {
            return null;
        }

        // 读取文件内容并尝试匹配"accountid"字段
        var content = File.ReadAllText(vdfPath);
        var match = Regex.Match(content, "\"accountid\"\\s*\"(?<id>\\d+)\"", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups["id"].Value;
        }

        // 若未找到标准格式，则尝试匹配任意长数字串（6位及以上）
        match = Regex.Match(content, "\"(?<id>\\d{6,})\"");
        return match.Success ? match.Groups["id"].Value : null;
    }

    /// <summary>
    /// 构建候选密钥列表，包括手动输入密钥、设备标识、目录名和Steam账户信息等。
    /// </summary>
    /// <param name="packagePath">存档包路径。</param>
    /// <param name="preferredKey">用户手动指定的首选密钥。</param>
    /// <param name="unityDeviceId">Unity设备标识。</param>
    /// <returns>包含密钥及其来源的元组列表。</returns>
    private static IReadOnlyList<(string Key, string Source)> BuildCandidates(string packagePath, string? preferredKey, string? unityDeviceId)
    {
        var list = new List<(string Key, string Source)>();
        
        // 定义本地函数，用于添加不重复且非空的密钥
        void Add(string? key, string source)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            // 避免重复添加相同密钥
            if (list.Any(x => string.Equals(x.Key, key, StringComparison.Ordinal)))
            {
                return;
            }

            list.Add((key.Trim(), source));
        }

        // 添加用户手动输入的密钥
        Add(preferredKey, "手动输入密钥");

        // 添加Unity设备标识
        Add(unityDeviceId, "Unity设备标识");

        // 尝试使用存档包的目录名作为候选密钥
        var folderName = Path.GetFileName(Path.GetFullPath(packagePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        Add(folderName, "目录名");

        // 从Steam云配置文件中提取账户ID并生成SteamID64
        var steamVdf = Path.Combine(packagePath, "steam_autocloud.vdf");
        if (File.Exists(steamVdf))
        {
            var content = File.ReadAllText(steamVdf);
            var match = Regex.Match(content, "\"accountid\"\\s*\"(?<id>\\d+)\"", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var accountIdText = match.Groups["id"].Value;
                Add(accountIdText, "steam_autocloud.vdf/accountid");
                // 将32位账户ID转换为64位SteamID格式
                if (ulong.TryParse(accountIdText, out var accountId))
                {
                    const ulong steamBase = 76561197960265728UL;
                    Add((steamBase + accountId).ToString(), "steam_autocloud.vdf/steamid64");
                }
            }
        }

        // 向上遍历最多4层父目录，尝试查找数字ID格式的目录名
        var directoryInfo = new DirectoryInfo(packagePath);
        for (var i = 0; i < 4 && directoryInfo is not null; i++, directoryInfo = directoryInfo.Parent)
        {
            var name = directoryInfo.Name;
            if (Regex.IsMatch(name, "^\\d{6,}$"))
            {
                Add(name, "父目录数字ID");
            }
        }

        // 添加Epic平台的常见回退密钥
        Add(EpicFallbackKey, "Epic常见密钥");
        return list;
    }
}
