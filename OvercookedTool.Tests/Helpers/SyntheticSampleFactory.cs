using System.Text;
using System.Text.Json.Nodes;
using OvercookedTool.Core.Crypto;
using OvercookedTool.Core.Services;

namespace OvercookedTool.Tests.Helpers;

/// <summary>
/// 合成 OC2 存档包工厂：在临时目录生成完整、可解密、CRC 校验通过的脱敏存档包，
/// 供集成测试在 CI 中无需任何真实账户数据即可运行。
/// </summary>
internal static class SyntheticSampleFactory
{
    /// <summary>
    /// 合成样本使用的虚假 SteamID64 密钥（目录名即密钥），与真实账户无关。
    /// </summary>
    public const string SteamId64Key = "76561198000000002";

    /// <summary>
    /// SteamID64 转 32 位 accountid 的基数。
    /// </summary>
    private const ulong SteamBase = 76561197960265728UL;

    /// <summary>
    /// 与 <see cref="SteamId64Key"/> 对应的 32 位 accountid
    /// （= 76561198000000002 - 76561197960265728 = 39734274）。
    /// </summary>
    private static readonly ulong AccountId = ulong.Parse(SteamId64Key) - SteamBase;

    /// <summary>
    /// 在临时目录下创建一个完整的合成 OC2 存档包并返回其路径。
    /// 包目录名即为 <see cref="SteamId64Key"/>，模拟真实 Steam 存档目录结构。
    /// 所有 .save 文件均用 <see cref="SteamId64Key"/> 加密落盘，CRC 校验通过，
    /// 可用 <c>OvercookedCrypto.DecryptData(..., ignoreCrc:false)</c> 正常解密。
    /// </summary>
    public static string CreateOc2Package()
    {
        // 用唯一容器目录包裹 SteamId64Key 命名的包目录，避免并行/重复运行时目录名冲突
        var container = Path.Combine(Path.GetTempPath(), "oct-synth-" + Guid.NewGuid().ToString("N"));
        var packageDir = Path.Combine(container, SteamId64Key);
        Directory.CreateDirectory(packageDir);

        // steam_autocloud.vdf：含 accountid 字段，供 KeyDetector 探测密钥与好友代码
        File.WriteAllText(Path.Combine(packageDir, "steam_autocloud.vdf"), BuildSteamAutocloudVdf());

        // Meta 存档：空条目即可
        WriteEncryptedSave(packageDir, "Meta_SaveFile.save", BuildMetaJson());

        // 三个本体 CoopSlot 存档，ScoreStars 各异（4/3/2）
        WriteEncryptedSave(packageDir, "CoopSlot_SaveFile_0.save", BuildOc2SaveJson(levelCount: 3, stars: 4));
        WriteEncryptedSave(packageDir, "CoopSlot_SaveFile_1.save", BuildOc2SaveJson(levelCount: 3, stars: 3));
        WriteEncryptedSave(packageDir, "CoopSlot_SaveFile_2.save", BuildOc2SaveJson(levelCount: 3, stars: 2));

        // DLC 存档（DLC2/3/5/7/8），各含 1-2 个 Level_ 条目
        WriteEncryptedSave(packageDir, "DLC2_CoopSlot_SaveFile_0.save", BuildOc2SaveJson(levelCount: 2, stars: 3));
        WriteEncryptedSave(packageDir, "DLC3_CoopSlot_SaveFile_0.save", BuildOc2SaveJson(levelCount: 2, stars: 3));
        WriteEncryptedSave(packageDir, "DLC5_CoopSlot_SaveFile_0.save", BuildOc2SaveJson(levelCount: 1, stars: 2));
        WriteEncryptedSave(packageDir, "DLC7_CoopSlot_SaveFile_0.save", BuildOc2SaveJson(levelCount: 1, stars: 2));
        WriteEncryptedSave(packageDir, "DLC8_CoopSlot_SaveFile_0.save", BuildOc2SaveJson(levelCount: 1, stars: 1));

        return packageDir;
    }

    /// <summary>
    /// 安全删除临时目录（递归），同时尝试清理其唯一容器父目录。失败时吞异常，不影响测试结果。
    /// </summary>
    public static void Cleanup(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var container = Path.GetDirectoryName(normalized);

        SafeDelete(normalized);
        if (!string.IsNullOrEmpty(container))
        {
            // 容器目录为本工厂创建的唯一父目录，删除包目录后一并清理
            SafeDelete(container);
        }
    }

    /// <summary>
    /// 生成紧凑无缩进的 OC2 关卡存档 JSON。
    /// 顶层含 m_Keys（Level_1..Level_N）与等长 m_Entries；每个 Level_ 条目内层为
    /// <code>{"m_Key":["ScoreStars","Completed"],"m_Value":["&lt;stars&gt;","True"]}</code>。
    /// 不含 "AssistModeEnabled" 顶层 key，<see cref="SaveJsonConverter.DetectVersion"/> 返回 <c>Oc2</c>。
    /// </summary>
    private static string BuildOc2SaveJson(int levelCount, int stars)
    {
        var keys = new JsonArray();
        var entries = new JsonArray();
        for (var i = 1; i <= levelCount; i++)
        {
            keys.Add($"Level_{i}");
            var inner = new JsonObject
            {
                ["m_Key"] = new JsonArray("ScoreStars", "Completed"),
                ["m_Value"] = new JsonArray(stars.ToString(), "True"),
            };
            // m_JSON 字段是内层对象的序列化字符串（紧凑、无缩进）
            entries.Add(new JsonObject { ["m_JSON"] = inner.ToJsonString() });
        }

        var root = new JsonObject
        {
            ["m_Keys"] = keys,
            ["m_Entries"] = entries,
        };
        return root.ToJsonString();
    }

    /// <summary>
    /// 生成 Meta 存档 JSON（空条目）。
    /// </summary>
    private static string BuildMetaJson()
    {
        var root = new JsonObject
        {
            ["m_Keys"] = new JsonArray(),
            ["m_Entries"] = new JsonArray(),
        };
        return root.ToJsonString();
    }

    /// <summary>
    /// 生成 steam_autocloud.vdf 内容，含与 <see cref="SteamId64Key"/> 对应的 accountid。
    /// 格式匹配 KeyDetector 的正则 <c>"accountid"\s*"(?&lt;id&gt;\d+)"</c>。
    /// </summary>
    private static string BuildSteamAutocloudVdf()
    {
        return $"\"steam_autocloud\"{Environment.NewLine}{{{Environment.NewLine}\"accountid\"\t\t\"{AccountId}\"{Environment.NewLine}}}{Environment.NewLine}";
    }

    /// <summary>
    /// 将 JSON 用 <see cref="SteamId64Key"/> 加密后写入指定存档文件。
    /// </summary>
    private static void WriteEncryptedSave(string packageDir, string fileName, string json)
    {
        var encrypted = OvercookedCrypto.EncryptData(Encoding.UTF8.GetBytes(json), SteamId64Key)
                        ?? throw new InvalidOperationException($"合成存档 {fileName} 加密失败");
        File.WriteAllBytes(Path.Combine(packageDir, fileName), encrypted);
    }

    private static void SafeDelete(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch
        {
            // 临时目录清理失败不影响测试结果
        }
    }
}
