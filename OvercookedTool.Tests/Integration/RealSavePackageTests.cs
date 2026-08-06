using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OvercookedTool.Core.Crypto;
using OvercookedTool.Core.Models;
using OvercookedTool.Core.Services;
using OvercookedTool.Tests.Helpers;

namespace OvercookedTool.Tests.Integration;

/// <summary>
/// 集成测试：基于 <see cref="SyntheticSampleFactory"/> 生成的脱敏合成 OC2 存档包。
///
/// 合成包内所有文件均可用 <see cref="SyntheticSampleFactory.SteamId64Key"/> 解密并通过 CRC 校验，
/// 不依赖任何真实账户数据，可在 CI 中稳定运行。每个测试实例在临时目录创建独立的合成包，
/// 测试结束自动清理。
/// </summary>
public class RealSavePackageTests : IDisposable
{
    private readonly string _packageDir;

    public RealSavePackageTests()
    {
        // 每个测试实例创建独立的合成包，保证测试间隔离
        _packageDir = SyntheticSampleFactory.CreateOc2Package();
    }

    public void Dispose()
    {
        SyntheticSampleFactory.Cleanup(_packageDir);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 通过一次性探测包枚举合成目录下所有 .save 文件名。
    /// 由于 <see cref="SyntheticSampleFactory.CreateOc2Package"/> 生成的文件名是确定性的，
    /// 探测得到的清单与每个测试实例创建的包内容一致。
    /// </summary>
    private static readonly Lazy<string[]> SaveFileNames = new(() =>
    {
        var probe = SyntheticSampleFactory.CreateOc2Package();
        try
        {
            return Directory.GetFiles(probe, "*.save", SearchOption.TopDirectoryOnly)
                .Select(f => Path.GetFileName(f) ?? string.Empty)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            SyntheticSampleFactory.Cleanup(probe);
        }
    });

    /// <summary>
    /// 合成目录下所有 .save 文件，用于参数化测试。
    /// </summary>
    public static IEnumerable<object[]> AllSaveFiles =>
        SaveFileNames.Value.Select(n => new object[] { n });

    /// <summary>
    /// 合成目录下所有非 Meta 的 .save 文件。
    /// </summary>
    public static IEnumerable<object[]> NonMetaSaveFiles =>
        SaveFileNames.Value
            .Where(n => !n.StartsWith("Meta", StringComparison.OrdinalIgnoreCase))
            .Select(n => new object[] { n });

    /// <summary>
    /// 每一个 .save 都应该通过独立的 CRC32 校验（不依赖密钥即可验证文件未损坏）。
    /// 使用独立实现的 CRC32（与 OvercookedCrypto 内部算法一致）做交叉验证。
    /// </summary>
    [Theory]
    [MemberData(nameof(AllSaveFiles))]
    public void EachSaveFile_Crc32Matches_UsingIndependentCrc32(string fileName)
    {
        var path = Path.Combine(_packageDir, fileName);
        var bytes = File.ReadAllBytes(path);

        // 至少需要 4 字节存放 CRC
        Assert.True(bytes.Length > 4, $"文件 {fileName} 太小");

        var size = (uint)(bytes.Length - 4);
        var expected = ComputeCrc32(bytes, 0, size);
        var stored = BitConverter.ToUInt32(bytes, bytes.Length - 4);
        Assert.Equal(expected, stored);
    }

    /// <summary>
    /// 独立实现的 CRC32（与 OvercookedCrypto.Crc32 算法一致：poly=1491524015, seed=3605721660）。
    /// </summary>
    private static uint ComputeCrc32(byte[] data, uint start, uint size)
    {
        const uint poly = 1491524015u;
        const uint seed = 3605721660u;
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var v = i;
            for (var b = 0; b < 8; b++)
            {
                v = (v & 1) != 1 ? v >> 1 : v ^ poly;
            }
            table[i] = v;
        }
        var hash = seed;
        for (uint i = start; i < start + size; i++)
        {
            hash = (hash >> 8) ^ table[data[i] ^ (hash & 0xFF)];
        }
        return hash;
    }

    /// <summary>
    /// 每一个 .save 都应该解出有效 JSON。合成包所有文件均可解密。
    /// </summary>
    [Theory]
    [MemberData(nameof(AllSaveFiles))]
    public void EachSave_ProducesValidJson(string fileName)
    {
        var path = Path.Combine(_packageDir, fileName);
        var bytes = File.ReadAllBytes(path);

        var ok = OvercookedCrypto.TryDecryptToJsonText(bytes, SyntheticSampleFactory.SteamId64Key, out var json, ignoreCrc: false);

        Assert.True(ok, $"文件 {fileName} 解密失败或 JSON 校验失败");
        Assert.False(string.IsNullOrWhiteSpace(json));

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    /// <summary>
    /// 所有非 Meta 存档都应被检测为 Oc2 版本。
    /// </summary>
    [Theory]
    [MemberData(nameof(NonMetaSaveFiles))]
    public void EachNonMetaSave_DetectedAsOc2(string fileName)
    {
        var path = Path.Combine(_packageDir, fileName);
        var bytes = File.ReadAllBytes(path);

        var ok = OvercookedCrypto.TryDecryptToJsonText(bytes, SyntheticSampleFactory.SteamId64Key, out var json, ignoreCrc: false);
        Assert.True(ok);

        Assert.Equal(SaveVersion.Oc2, SaveJsonConverter.DetectVersion(json));
    }

    /// <summary>
    /// 加解密往返：解密 -> 加密 -> 解密 应保持 JSON 内容一致。
    /// </summary>
    [Fact]
    public void EncryptDecrypt_RoundTrip_OnRealSave_PreservesJsonContent()
    {
        var fileName = "CoopSlot_SaveFile_0.save";
        var path = Path.Combine(_packageDir, fileName);
        var bytes = File.ReadAllBytes(path);

        var plain1 = OvercookedCrypto.DecryptData(bytes, SyntheticSampleFactory.SteamId64Key, ignoreCrc: false);
        Assert.NotNull(plain1);
        var json1 = Encoding.UTF8.GetString(plain1!);

        // 用同一密钥重新加密（IV 会随机生成）
        var reEncrypted = OvercookedCrypto.EncryptData(plain1!, SyntheticSampleFactory.SteamId64Key);
        Assert.NotNull(reEncrypted);

        // 再次解密应得到相同的 JSON 内容
        var plain2 = OvercookedCrypto.DecryptData(reEncrypted!, SyntheticSampleFactory.SteamId64Key, ignoreCrc: false);
        Assert.NotNull(plain2);
        var json2 = Encoding.UTF8.GetString(plain2!);

        Assert.Equal(json1, json2);
    }

    /// <summary>
    /// 验证修复后的 PKCS7 处理：游戏原始密文使用 PKCS7 填充，项目 DecryptData 应正确去除填充，
    /// 重新加密后的密文长度应与原始一致（不再多 16 字节）。
    /// </summary>
    /// <remarks>
    /// 修复前的 bug：DecryptData 用 CryptoStream.Read 写入 output = new byte[cipher.Length]，
    /// 缓冲区比真实明文长 1-16 字节（尾随 0x00）。重新加密时把这个含尾随零的缓冲区当作明文加密，
    /// PKCS7 又加 16 字节填充，导致重新加密的密文比原始多 16 字节。
    ///
    /// 修复方案：用 TransformFinalBlock 替代 CryptoStream.Read，自动去除 PKCS7 填充，
    /// 返回真实明文长度。重新加密后密文长度与原始一致。
    /// </remarks>
    [Fact]
    public void Padding_GameUsesPkcs7_ReencryptedFileHasSameCipherLength()
    {
        var fileName = "CoopSlot_SaveFile_0.save";
        var path = Path.Combine(_packageDir, fileName);
        var bytes = File.ReadAllBytes(path);

        var plain = OvercookedCrypto.DecryptData(bytes, SyntheticSampleFactory.SteamId64Key, ignoreCrc: false)!;
        var reEncrypted = OvercookedCrypto.EncryptData(plain, SyntheticSampleFactory.SteamId64Key)!;

        // 原始密文长度 = 文件大小 - 16 (IV) - 4 (CRC)
        var originalCipherLen = bytes.Length - 16 - 4;
        // 重新加密的密文长度 = 重新加密文件大小 - 16 (IV) - 4 (CRC)
        var reEncryptedCipherLen = reEncrypted.Length - 16 - 4;

        // 1. 原始密文是 16 的倍数（AES-CBC 块对齐要求，PKCS7 必然满足）
        Assert.Equal(0, originalCipherLen % 16);

        // 2. 重新加密的密文也是 16 的倍数
        Assert.Equal(0, reEncryptedCipherLen % 16);

        // 3. 修复后：重新加密的密文长度应与原始一致（不再多 16 字节）
        Assert.Equal(originalCipherLen, reEncryptedCipherLen);

        // 4. 验证 DecryptData 返回的明文长度 < cipher 长度（已去除 PKCS7 填充）
        Assert.True(plain.Length < originalCipherLen,
            $"修复后明文长度 {plain.Length} 应小于 cipher 长度 {originalCipherLen}（已去除 PKCS7 填充）");
        Assert.True(plain.Length > 0);

        // 5. 验证明文是有效 JSON
        var jsonText = Encoding.UTF8.GetString(plain);
        using var doc = JsonDocument.Parse(jsonText);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);

        // 6. 验证明文末尾不是 0x00（不应有尾随零）
        Assert.NotEqual((byte)'\0', plain[^1]);
    }

    /// <summary>
    /// 转换往返：Oc2 → Ayce → Oc2 应保留所有 Level_ 条目的核心字段（除 FailedAttempts 外）。
    /// </summary>
    [Fact]
    public void Convert_RoundTrip_Oc2ToAyceToOc2_PreservesLevelFields()
    {
        var fileName = "CoopSlot_SaveFile_0.save";
        var path = Path.Combine(_packageDir, fileName);
        var bytes = File.ReadAllBytes(path);
        var ok = OvercookedCrypto.TryDecryptToJsonText(bytes, SyntheticSampleFactory.SteamId64Key, out var originalJson, ignoreCrc: false);
        Assert.True(ok);

        var toAyce = SaveJsonConverter.Convert(originalJson, SaveVersion.Oc2, SaveVersion.Ayce);
        Assert.Equal(SaveVersion.Ayce, SaveJsonConverter.DetectVersion(toAyce));

        var backToOc2 = SaveJsonConverter.Convert(toAyce, SaveVersion.Ayce, SaveVersion.Oc2);
        Assert.Equal(SaveVersion.Oc2, SaveJsonConverter.DetectVersion(backToOc2));

        var origRoot = JsonNode.Parse(originalJson)!.AsObject();
        var backRoot = JsonNode.Parse(backToOc2)!.AsObject();
        var origKeys = origRoot["m_Keys"]!.AsArray();
        var origEntries = origRoot["m_Entries"]!.AsArray();
        var backKeys = backRoot["m_Keys"]!.AsArray();
        var backEntries = backRoot["m_Entries"]!.AsArray();

        Assert.Equal(origKeys.Count, backKeys.Count);

        for (var i = 0; i < origKeys.Count; i++)
        {
            var key = origKeys[i]!.GetValue<string>();
            Assert.Equal(key, backKeys[i]!.GetValue<string>());

            if (!key.StartsWith("Level_", StringComparison.Ordinal)) continue;

            var origInner = JsonNode.Parse(origEntries[i]!["m_JSON"]!.GetValue<string>())!.AsObject();
            var backInner = JsonNode.Parse(backEntries[i]!["m_JSON"]!.GetValue<string>())!.AsObject();
            var origMap = ExtractMap(origInner);
            var backMap = ExtractMap(backInner);

            Assert.False(origMap.ContainsKey("FailedAttempts"));
            Assert.False(backMap.ContainsKey("FailedAttempts"));

            foreach (var pair in origMap)
            {
                Assert.True(backMap.TryGetValue(pair.Key, out var v), $"字段 {pair.Key} 应保留");
                Assert.Equal(pair.Value, v);
            }
        }
    }

    /// <summary>
    /// 加密后的存档字节布局与原文件结构一致：IV(16) + 密文 + CRC(4)，密文为 16 的倍数。
    /// </summary>
    [Fact]
    public void ReEncryptedSave_HasValidLayout_IvPlusCipherPlusCrc()
    {
        var fileName = "CoopSlot_SaveFile_0.save";
        var path = Path.Combine(_packageDir, fileName);
        var bytes = File.ReadAllBytes(path);

        var plain = OvercookedCrypto.DecryptData(bytes, SyntheticSampleFactory.SteamId64Key, ignoreCrc: false)!;
        var reEncrypted = OvercookedCrypto.EncryptData(plain, SyntheticSampleFactory.SteamId64Key)!;

        Assert.True(reEncrypted.Length > 16 + 4);
        Assert.Equal(0, (reEncrypted.Length - 16 - 4) % 16);
    }

    // ===== SavePackageService 端到端 =====

    /// <summary>
    /// 端到端：SavePackageService 能加载合成包目录，识别为 Oc2Binary 平台、Oc2 版本、密钥校验通过，
    /// 并从 steam_autocloud.vdf 提取出与 SteamID64 对应的 accountid 好友代码。
    /// </summary>
    [Fact]
    public void SavePackageService_LoadPackage_OnRealSample_RecognizesOc2BinaryAndSteamId64Key()
    {
        var service = new SavePackageService();
        var package = service.LoadPackage(_packageDir);

        Assert.Equal(SavePlatform.Oc2Binary, package.Platform);
        Assert.Equal(SaveVersion.Oc2, package.Version);
        Assert.Equal(SyntheticSampleFactory.SteamId64Key, package.DetectedKey);
        Assert.True(package.KeyValidated);
        Assert.NotEmpty(package.Saves);
        Assert.Contains(package.Saves, x => !x.IsMeta);
        Assert.Contains(package.Saves, x => x.IsMeta);
        // FriendCode 应为 steam_autocloud.vdf 中的 accountid，
        // 即 SteamID64 - 76561197960265728 = 39734274
        var expectedAccountId = (ulong.Parse(SyntheticSampleFactory.SteamId64Key) - 76561197960265728UL).ToString();
        Assert.Equal(expectedAccountId, package.FriendCode);
    }

    /// <summary>
    /// 所有非 Meta 存档都应该能通过 SavePackageService.ReadSaveAsJson 读出有效 JSON。
    /// </summary>
    [Theory]
    [MemberData(nameof(NonMetaSaveFiles))]
    public void SavePackageService_ReadSaveAsJson_AllNonMetaSucceed(string fileName)
    {
        var service = new SavePackageService();
        var package = service.LoadPackage(_packageDir);
        var save = package.Saves.FirstOrDefault(x => string.Equals(x.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(save);

        var json = service.ReadSaveAsJson(package, save!);
        Assert.False(string.IsNullOrWhiteSpace(json));
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    /// <summary>
    /// 所有非 Meta 存档的星级统计应被正确填充（非 null 且 >= 0）。
    /// </summary>
    [Theory]
    [MemberData(nameof(NonMetaSaveFiles))]
    public void SavePackageService_StarCount_PopulatedForNonMetaSaves(string fileName)
    {
        var service = new SavePackageService();
        var package = service.LoadPackage(_packageDir);
        var save = package.Saves.FirstOrDefault(x => string.Equals(x.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(save);
        Assert.NotNull(save!.StarCount);
        Assert.True(save.StarCount >= 0);
    }

    /// <summary>
    /// DLC 分组应该被正确识别。合成包含 DLC2/3/5/7/8。
    /// </summary>
    [Fact]
    public void SavePackageService_DlcGroups_AreCorrectlyIdentifiedFromFilenames()
    {
        var service = new SavePackageService();
        var package = service.LoadPackage(_packageDir);

        var dlcIds = package.Saves
            .Where(x => x.DlcId.HasValue)
            .Select(x => x.DlcId!.Value)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        // 文件名解析应识别出 DLC2/3/5/7/8
        Assert.Contains(2, dlcIds);
        Assert.Contains(3, dlcIds);
        Assert.Contains(5, dlcIds);
        Assert.Contains(7, dlcIds);
        Assert.Contains(8, dlcIds);
    }

    /// <summary>
    /// 写回测试：读出 JSON -> 原样写回 -> 再读出应一致（不破坏存档结构）。
    /// 临时目录没有 steam_autocloud.vdf，需显式传入密钥。
    /// </summary>
    [Fact]
    public void SavePackageService_WriteBack_PreservesJsonContent()
    {
        var service = new SavePackageService();
        var package = service.LoadPackage(_packageDir);

        var tempDir = Path.Combine(Path.GetTempPath(), "oct-writeback-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            // 选一个非 Meta 存档（合成包内所有文件均可解密）
            var src = package.Saves.First(x => !x.IsMeta);
            var json = service.ReadSaveAsJson(package, src);
            var key = package.DetectedKey ?? throw new InvalidOperationException("样本包应已探测到密钥");

            var targetPath = Path.Combine(tempDir, src.FileName);
            File.WriteAllBytes(targetPath, OvercookedCrypto.EncryptData(Encoding.UTF8.GetBytes(json), key)!);

            // 重新加载临时目录时显式传入密钥（临时目录没有 steam_autocloud.vdf，无法自动探测）
            var repkg = service.LoadPackage(tempDir, preferredKey: key);
            Assert.Equal(SavePlatform.Oc2Binary, repkg.Platform);
            Assert.Equal(SaveVersion.Oc2, repkg.Version);
            var reJson = service.ReadSaveAsJson(repkg, repkg.Saves.First(x => !x.IsMeta));

            Assert.Equal(json, reJson);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    // ===== Helpers =====

    private static Dictionary<string, string> ExtractMap(JsonObject inner)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var keys = inner["m_Key"] as JsonArray;
        var values = inner["m_Value"] as JsonArray;
        if (keys is null || values is null) return result;
        var count = Math.Min(keys.Count, values.Count);
        for (var i = 0; i < count; i++)
        {
            var k = keys[i]?.GetValue<string>();
            if (string.IsNullOrEmpty(k)) continue;
            result[k!] = values[i]?.ToString() ?? string.Empty;
        }
        return result;
    }
}
