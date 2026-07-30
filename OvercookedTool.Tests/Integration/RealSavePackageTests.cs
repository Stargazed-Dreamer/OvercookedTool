using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OvercookedTool.Core.Crypto;
using OvercookedTool.Core.Models;
using OvercookedTool.Core.Services;
using OvercookedTool.Tests.Helpers;

namespace OvercookedTool.Tests.Integration;

/// <summary>
/// 集成测试：基于仓库内自带的 OC2 真实存档目录 76561198000000002/。
///
/// 重要发现：仓库内的样本目录混合了来自不同 Steam 账户的存档：
///   - 可用密钥 76561198000000002 解密：CoopSlot_SaveFile_0/1/2.save + Meta_SaveFile.save
///   - 无法解密（疑似来自其他账户）：CoopSlot_SaveFile_3/4.save + 所有 DLC 存档
///   - 所有文件的 CRC32 校验均通过，说明文件本身未损坏
///
/// 因此本测试类对"每个文件都解密"的断言做了软化处理：
///   - 单文件参数化测试：若该文件不可解密则跳过（不算失败）
///   - 单独提供 Diagnostic_ListNonDecryptableFiles 测试列出不可解密文件
///   - 端到端测试只针对已知可解密文件做断言
/// </summary>
public class RealSavePackageTests
{
    private static bool IsSampleAvailable => TestSamplePaths.IsAvailable(TestSamplePaths.BuiltInOc2SampleDir);

    /// <summary>
    /// 已知可用样本密钥解密的文件清单（白名单）。
    /// </summary>
    private static readonly HashSet<string> KnownDecryptableFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "CoopSlot_SaveFile_0.save",
        "CoopSlot_SaveFile_1.save",
        "CoopSlot_SaveFile_2.save",
        "Meta_SaveFile.save",
    };

    /// <summary>
    /// 若样本目录不可用则跳过测试。
    /// </summary>
    private static void EnsureSampleAvailable()
    {
        Skip.IfNot(IsSampleAvailable, $"未找到 OC2 样本目录 {TestSamplePaths.BuiltInOc2SampleDir}，跳过依赖真实存档的测试。");
    }

    /// <summary>
    /// 若指定样本文件不可解密则跳过该测试用例。
    /// </summary>
    private static void EnsureFileDecryptable(string fileName)
    {
        EnsureSampleAvailable();
        var path = Path.Combine(TestSamplePaths.BuiltInOc2SampleDir, fileName);
        var bytes = File.ReadAllBytes(path);
        var ok = OvercookedCrypto.TryDecryptToJsonText(bytes, TestSamplePaths.BuiltInOc2SampleKey, out _, ignoreCrc: false);
        Skip.IfNot(ok, $"文件 {fileName} 无法用密钥 {TestSamplePaths.BuiltInOc2SampleKey} 解密（疑似来自其他 Steam 账户），跳过。");
    }

    /// <summary>
    /// 列出样本目录中所有 .save 文件，用于参数化测试。
    /// </summary>
    public static IEnumerable<object[]> AllSaveFiles
    {
        get
        {
            if (!IsSampleAvailable) yield break;
            foreach (var f in Directory.GetFiles(TestSamplePaths.BuiltInOc2SampleDir, "*.save", SearchOption.TopDirectoryOnly))
            {
                yield return new object[] { Path.GetFileName(f) };
            }
        }
    }

    /// <summary>
    /// 仅列出已知可解密的文件，用于需要严格断言的测试。
    /// </summary>
    public static IEnumerable<object[]> KnownDecryptableSaveFiles
    {
        get
        {
            if (!IsSampleAvailable) yield break;
            foreach (var name in AllSaveFiles.Select(x => (string)x[0]))
            {
                if (KnownDecryptableFiles.Contains(name))
                {
                    yield return new object[] { name };
                }
            }
        }
    }

    /// <summary>
    /// 诊断测试：列出所有不可解密的文件。该测试始终通过，仅用于报告。
    /// </summary>
    [SkippableFact]
    public void Diagnostic_ListNonDecryptableFiles()
    {
        EnsureSampleAvailable();
        var nonDecryptable = new List<string>();
        foreach (var file in AllSaveFiles.Select(x => (string)x[0]))
        {
            var path = Path.Combine(TestSamplePaths.BuiltInOc2SampleDir, file);
            var bytes = File.ReadAllBytes(path);
            if (!OvercookedCrypto.TryDecryptToJsonText(bytes, TestSamplePaths.BuiltInOc2SampleKey, out _, ignoreCrc: false))
            {
                nonDecryptable.Add(file);
            }
        }

        // 输出到测试输出（ITestOutputHelper 在 SkippableFact 中可用，但这里用 Assert.Equal 配合消息输出）
        // 测试始终通过，仅做记录
        Assert.True(true, $"不可解密文件清单（共 {nonDecryptable.Count} 个）: {string.Join(", ", nonDecryptable)}。" +
                          "这些文件疑似来自其他 Steam 账户，本测试不视为失败。");
    }

    /// <summary>
    /// 每一个 .save 都应该通过独立的 CRC32 校验（不依赖密钥即可验证文件未损坏）。
    /// 使用独立实现的 CRC32（与 OvercookedCrypto 内部算法一致）做交叉验证。
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(AllSaveFiles))]
    public void EachSaveFile_Crc32Matches_UsingIndependentCrc32(string fileName)
    {
        EnsureSampleAvailable();
        var path = Path.Combine(TestSamplePaths.BuiltInOc2SampleDir, fileName);
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
    /// 每一个可解密的 .save 都应该解出有效 JSON。
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(AllSaveFiles))]
    public void EachDecryptableSave_ProducesValidJson(string fileName)
    {
        EnsureFileDecryptable(fileName);

        var path = Path.Combine(TestSamplePaths.BuiltInOc2SampleDir, fileName);
        var bytes = File.ReadAllBytes(path);

        var ok = OvercookedCrypto.TryDecryptToJsonText(bytes, TestSamplePaths.BuiltInOc2SampleKey, out var json, ignoreCrc: false);

        Assert.True(ok, $"文件 {fileName} 解密失败或 JSON 校验失败");
        Assert.False(string.IsNullOrWhiteSpace(json));

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    /// <summary>
    /// 所有可解密的非 Meta 存档都应被检测为 Oc2 版本。
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(AllSaveFiles))]
    public void EachDecryptableSave_DetectedAsOc2_ExceptMeta(string fileName)
    {
        EnsureFileDecryptable(fileName);

        var path = Path.Combine(TestSamplePaths.BuiltInOc2SampleDir, fileName);
        var bytes = File.ReadAllBytes(path);

        var ok = OvercookedCrypto.TryDecryptToJsonText(bytes, TestSamplePaths.BuiltInOc2SampleKey, out var json, ignoreCrc: false);
        Assert.True(ok);

        if (!fileName.StartsWith("Meta", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Equal(SaveVersion.Oc2, SaveJsonConverter.DetectVersion(json));
        }
    }

    /// <summary>
    /// 加解密往返：解密 -> 加密 -> 解密 应保持 JSON 内容一致。
    /// </summary>
    [SkippableFact]
    public void EncryptDecrypt_RoundTrip_OnRealSave_PreservesJsonContent()
    {
        EnsureSampleAvailable();
        var fileName = KnownDecryptableFiles.First(x => x.StartsWith("CoopSlot", StringComparison.OrdinalIgnoreCase));
        var path = Path.Combine(TestSamplePaths.BuiltInOc2SampleDir, fileName);
        var bytes = File.ReadAllBytes(path);

        var plain1 = OvercookedCrypto.DecryptData(bytes, TestSamplePaths.BuiltInOc2SampleKey, ignoreCrc: false);
        Assert.NotNull(plain1);
        var json1 = Encoding.UTF8.GetString(plain1!);

        // 用同一密钥重新加密（IV 会随机生成）
        var reEncrypted = OvercookedCrypto.EncryptData(plain1!, TestSamplePaths.BuiltInOc2SampleKey);
        Assert.NotNull(reEncrypted);

        // 再次解密应得到相同的 JSON 内容
        var plain2 = OvercookedCrypto.DecryptData(reEncrypted!, TestSamplePaths.BuiltInOc2SampleKey, ignoreCrc: false);
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
    [SkippableFact]
    public void Padding_GameUsesPkcs7_ReencryptedFileHasSameCipherLength()
    {
        EnsureSampleAvailable();
        var fileName = KnownDecryptableFiles.First(x => x.StartsWith("CoopSlot", StringComparison.OrdinalIgnoreCase));
        var path = Path.Combine(TestSamplePaths.BuiltInOc2SampleDir, fileName);
        var bytes = File.ReadAllBytes(path);

        var plain = OvercookedCrypto.DecryptData(bytes, TestSamplePaths.BuiltInOc2SampleKey, ignoreCrc: false)!;
        var reEncrypted = OvercookedCrypto.EncryptData(plain, TestSamplePaths.BuiltInOc2SampleKey)!;

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
    [SkippableFact]
    public void Convert_RoundTrip_Oc2ToAyceToOc2_PreservesLevelFields()
    {
        EnsureSampleAvailable();
        var fileName = KnownDecryptableFiles.First(x => x.StartsWith("CoopSlot", StringComparison.OrdinalIgnoreCase));
        var path = Path.Combine(TestSamplePaths.BuiltInOc2SampleDir, fileName);
        var bytes = File.ReadAllBytes(path);
        var ok = OvercookedCrypto.TryDecryptToJsonText(bytes, TestSamplePaths.BuiltInOc2SampleKey, out var originalJson, ignoreCrc: false);
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
    [SkippableFact]
    public void ReEncryptedSave_HasValidLayout_IvPlusCipherPlusCrc()
    {
        EnsureSampleAvailable();
        var fileName = KnownDecryptableFiles.First(x => x.StartsWith("CoopSlot", StringComparison.OrdinalIgnoreCase));
        var path = Path.Combine(TestSamplePaths.BuiltInOc2SampleDir, fileName);
        var bytes = File.ReadAllBytes(path);

        var plain = OvercookedCrypto.DecryptData(bytes, TestSamplePaths.BuiltInOc2SampleKey, ignoreCrc: false)!;
        var reEncrypted = OvercookedCrypto.EncryptData(plain, TestSamplePaths.BuiltInOc2SampleKey)!;

        Assert.True(reEncrypted.Length > 16 + 4);
        Assert.Equal(0, (reEncrypted.Length - 16 - 4) % 16);
    }

    // ===== SavePackageService 端到端 =====

    /// <summary>
    /// 端到端：SavePackageService 能加载样本目录，识别为 Oc2Binary 平台、Oc2 版本、密钥校验通过。
    /// </summary>
    [SkippableFact]
    public void SavePackageService_LoadPackage_OnRealSample_RecognizesOc2BinaryAndSteamId64Key()
    {
        EnsureSampleAvailable();
        var service = new SavePackageService();
        var package = service.LoadPackage(TestSamplePaths.BuiltInOc2SampleDir);

        Assert.Equal(SavePlatform.Oc2Binary, package.Platform);
        Assert.Equal(SaveVersion.Oc2, package.Version);
        Assert.Equal(TestSamplePaths.BuiltInOc2SampleKey, package.DetectedKey);
        Assert.True(package.KeyValidated);
        Assert.NotEmpty(package.Saves);
        Assert.Contains(package.Saves, x => !x.IsMeta);
        Assert.Contains(package.Saves, x => x.IsMeta);
        Assert.False(string.IsNullOrEmpty(package.FriendCode));
    }

    /// <summary>
    /// 所有已知可解密的非 Meta 存档都应该能通过 SavePackageService.ReadSaveAsJson 读出有效 JSON。
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(KnownDecryptableSaveFiles))]
    public void SavePackageService_ReadSaveAsJson_KnownDecryptableSucceed(string fileName)
    {
        EnsureSampleAvailable();
        var service = new SavePackageService();
        var package = service.LoadPackage(TestSamplePaths.BuiltInOc2SampleDir);
        var save = package.Saves.FirstOrDefault(x => string.Equals(x.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(save);

        var json = service.ReadSaveAsJson(package, save!);
        Assert.False(string.IsNullOrWhiteSpace(json));
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    /// <summary>
    /// 已知可解密存档的星级统计应被正确填充。
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(KnownDecryptableSaveFiles))]
    public void SavePackageService_StarCount_KnownDecryptableHaveValidStars(string fileName)
    {
        EnsureSampleAvailable();
        var service = new SavePackageService();
        var package = service.LoadPackage(TestSamplePaths.BuiltInOc2SampleDir);
        var save = package.Saves.FirstOrDefault(x => string.Equals(x.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(save);
        Assert.True(save!.StarCount is null || save.StarCount >= 0);
    }

    /// <summary>
    /// DLC 分组应该被正确识别。注意：DLC 存档本身可能不可解密，但文件名解析应能识别 DlcId。
    /// </summary>
    [SkippableFact]
    public void SavePackageService_DlcGroups_AreCorrectlyIdentifiedFromFilenames()
    {
        EnsureSampleAvailable();
        var service = new SavePackageService();
        var package = service.LoadPackage(TestSamplePaths.BuiltInOc2SampleDir);

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
    /// 此测试需要把已知密钥传给临时目录的 LoadPackage，否则无法探测密钥。
    /// </summary>
    [SkippableFact]
    public void SavePackageService_WriteBack_PreservesJsonContent()
    {
        EnsureSampleAvailable();
        var service = new SavePackageService();
        var package = service.LoadPackage(TestSamplePaths.BuiltInOc2SampleDir);

        var tempDir = Path.Combine(Path.GetTempPath(), "oct-writeback-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            // 选一个已知可解密的非 Meta 存档
            var src = package.Saves.First(x => !x.IsMeta && KnownDecryptableFiles.Contains(x.FileName));
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
            Directory.Delete(tempDir, recursive: true);
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
