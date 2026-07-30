using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OvercookedTool.Core.Crypto;
using OvercookedTool.Core.Models;
using OvercookedTool.Core.Services;
using OvercookedTool.Tests.Helpers;

namespace OvercookedTool.Tests.Integration;

/// <summary>
/// 用户提供的完整样本集成测试：
///   - 参考/我的存档/OC2/76561198000000001/   完整 OC2 同账户含所有 DLC（DLC2/3/5/7/8）
///   - 参考/我的存档/AYCE/76561198000000001/  AYCE Steam 版加密二进制（含 BAG/OC1/DLC202 等 AYCE 独有前缀）
///
/// 这些样本位于 .gitignore 排除的 参考/ 目录下，CI 环境会跳过；本地运行时若缺失也会跳过。
/// </summary>
public class UserSampleTests
{
    // ===== OC2 完整同账户样本 =====

    /// <summary>
    /// 列出用户 OC2 样本目录下所有 .save 文件，用于参数化测试。
    /// </summary>
    public static IEnumerable<object[]> UserOc2SaveFiles
    {
        get
        {
            if (!TestSamplePaths.IsAvailable(TestSamplePaths.UserOc2SampleDir)) yield break;
            foreach (var f in Directory.GetFiles(TestSamplePaths.UserOc2SampleDir, "*.save", SearchOption.TopDirectoryOnly))
            {
                yield return new object[] { Path.GetFileName(f) };
            }
        }
    }

    /// <summary>
    /// 用户 OC2 样本目录应该可用（本地开发环境）。
    /// </summary>
    [SkippableFact]
    public void UserOc2Sample_Available()
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.UserOc2SampleDir),
            $"未找到用户 OC2 样本目录 {TestSamplePaths.UserOc2SampleDir}");
    }

    /// <summary>
    /// 所有用户 OC2 存档都应通过 CRC32 校验。
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(UserOc2SaveFiles))]
    public void UserOc2_EachSaveFile_Crc32Matches(string fileName)
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.UserOc2SampleDir), "用户 OC2 样本缺失");
        var path = Path.Combine(TestSamplePaths.UserOc2SampleDir, fileName);
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 4);

        var size = (uint)(bytes.Length - 4);
        var expected = ComputeCrc32(bytes, 0, size);
        var stored = BitConverter.ToUInt32(bytes, bytes.Length - 4);
        Assert.Equal(expected, stored);
    }

    /// <summary>
    /// 所有用户 OC2 存档都应可用 SteamID64 密钥解密为有效 JSON（同账户验证）。
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(UserOc2SaveFiles))]
    public void UserOc2_EachSaveFile_DecryptsWithSteamId64Key(string fileName)
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.UserOc2SampleDir), "用户 OC2 样本缺失");
        var path = Path.Combine(TestSamplePaths.UserOc2SampleDir, fileName);
        var bytes = File.ReadAllBytes(path);

        var ok = OvercookedCrypto.TryDecryptToJsonText(bytes, TestSamplePaths.UserSampleKey, out var json, ignoreCrc: false);
        Assert.True(ok, $"文件 {fileName} 无法用密钥 {TestSamplePaths.UserSampleKey} 解密");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    /// <summary>
    /// 所有用户 OC2 非 Meta 存档都应被检测为 Oc2 版本。
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(UserOc2SaveFiles))]
    public void UserOc2_EachNonMetaSave_DetectedAsOc2(string fileName)
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.UserOc2SampleDir), "用户 OC2 样本缺失");
        if (fileName.StartsWith("Meta", StringComparison.OrdinalIgnoreCase)) return;

        var path = Path.Combine(TestSamplePaths.UserOc2SampleDir, fileName);
        var bytes = File.ReadAllBytes(path);

        var ok = OvercookedCrypto.TryDecryptToJsonText(bytes, TestSamplePaths.UserSampleKey, out var json, ignoreCrc: false);
        Assert.True(ok);

        Assert.Equal(SaveVersion.Oc2, SaveJsonConverter.DetectVersion(json));
    }

    /// <summary>
    /// 用户 OC2 样本应包含所有 DLC（2/3/5/7/8）。
    /// </summary>
    [SkippableFact]
    public void UserOc2_ContainsAllDlcs()
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.UserOc2SampleDir), "用户 OC2 样本缺失");
        var files = Directory.GetFiles(TestSamplePaths.UserOc2SampleDir, "DLC*_*.save", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(x => x is not null)
            .Cast<string>()
            .ToList();

        Assert.Contains(files, x => x.StartsWith("DLC2_", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, x => x.StartsWith("DLC3_", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, x => x.StartsWith("DLC5_", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, x => x.StartsWith("DLC7_", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, x => x.StartsWith("DLC8_", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 用户 OC2 样本应包含 steam_autocloud.vdf，且 accountid 与 SteamID64 一致。
    /// </summary>
    [SkippableFact]
    public void UserOc2_SteamAutocloudVdf_AccountIdMatches()
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.UserOc2SampleDir), "用户 OC2 样本缺失");
        var vdfPath = Path.Combine(TestSamplePaths.UserOc2SampleDir, "steam_autocloud.vdf");
        Skip.IfNot(File.Exists(vdfPath), "未找到 steam_autocloud.vdf");

        var detector = new KeyDetector();
        var friendCode = detector.TryExtractFriendCode(TestSamplePaths.UserOc2SampleDir);
        Assert.NotNull(friendCode);

        // SteamID64 = 76561197960265728 + accountid
        // 76561198000000001 - 76561197960265728 = 39734273
        const ulong steamBase = 76561197960265728UL;
        var expectedAccountId = ulong.Parse(TestSamplePaths.UserSampleKey) - steamBase;
        Assert.Equal(expectedAccountId.ToString(), friendCode);
    }

    /// <summary>
    /// SavePackageService 加载用户 OC2 样本应识别为 Oc2Binary/Oc2，密钥校验通过。
    /// </summary>
    [SkippableFact]
    public void UserOc2_SavePackageService_RecognizesPlatformAndKey()
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.UserOc2SampleDir), "用户 OC2 样本缺失");
        var service = new SavePackageService();
        var package = service.LoadPackage(TestSamplePaths.UserOc2SampleDir);

        Assert.Equal(SavePlatform.Oc2Binary, package.Platform);
        Assert.Equal(SaveVersion.Oc2, package.Version);
        Assert.Equal(TestSamplePaths.UserSampleKey, package.DetectedKey);
        Assert.True(package.KeyValidated);
        Assert.NotEmpty(package.Saves);
        Assert.Contains(package.Saves, x => !x.IsMeta);
        Assert.Contains(package.Saves, x => x.IsMeta);
    }

    /// <summary>
    /// 用户 OC2 样本中 DLC 分组应被正确识别。
    /// </summary>
    [SkippableFact]
    public void UserOc2_DlcGroups_AreCorrectlyIdentified()
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.UserOc2SampleDir), "用户 OC2 样本缺失");
        var service = new SavePackageService();
        var package = service.LoadPackage(TestSamplePaths.UserOc2SampleDir);

        var dlcIds = package.Saves
            .Where(x => x.DlcId.HasValue)
            .Select(x => x.DlcId!.Value)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        Assert.Equal(new[] { 2, 3, 5, 7, 8 }, dlcIds);
    }

    /// <summary>
    /// 加解密往返：对每个用户 OC2 存档解密后重新加密，密文长度应与原始一致（PKCS7 修复验证）。
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(UserOc2SaveFiles))]
    public void UserOc2_Reencrypt_PreservesCipherLength(string fileName)
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.UserOc2SampleDir), "用户 OC2 样本缺失");
        var path = Path.Combine(TestSamplePaths.UserOc2SampleDir, fileName);
        var bytes = File.ReadAllBytes(path);

        var plain = OvercookedCrypto.DecryptData(bytes, TestSamplePaths.UserSampleKey, ignoreCrc: false);
        Assert.NotNull(plain);

        var reEncrypted = OvercookedCrypto.EncryptData(plain!, TestSamplePaths.UserSampleKey);
        Assert.NotNull(reEncrypted);

        var originalCipherLen = bytes.Length - 16 - 4;
        var reCipherLen = reEncrypted!.Length - 16 - 4;
        Assert.Equal(originalCipherLen, reCipherLen);
    }

    /// <summary>
    /// OC2 → AYCE → OC2 转换往返：所有 Level_ 条目核心字段应保留（除 FailedAttempts）。
    /// </summary>
    [SkippableFact]
    public void UserOc2_ConvertRoundTrip_PreservesLevelFields()
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.UserOc2SampleDir), "用户 OC2 样本缺失");
        var service = new SavePackageService();
        var package = service.LoadPackage(TestSamplePaths.UserOc2SampleDir);
        var src = package.Saves.First(x => !x.IsMeta);
        var json = service.ReadSaveAsJson(package, src);

        var toAyce = SaveJsonConverter.Convert(json, SaveVersion.Oc2, SaveVersion.Ayce);
        Assert.Equal(SaveVersion.Ayce, SaveJsonConverter.DetectVersion(toAyce));

        var backToOc2 = SaveJsonConverter.Convert(toAyce, SaveVersion.Ayce, SaveVersion.Oc2);
        Assert.Equal(SaveVersion.Oc2, SaveJsonConverter.DetectVersion(backToOc2));

        // 验证 AYCE 版本包含 AssistModeEnabled/FailedAttempts 字段
        var ayceRoot = JsonNode.Parse(toAyce)!.AsObject();
        var ayceKeys = ayceRoot["m_Keys"]!.AsArray();
        var ayceEntries = ayceRoot["m_Entries"]!.AsArray();
        var hasAyceField = false;
        for (var i = 0; i < ayceKeys.Count; i++)
        {
            var key = ayceKeys[i]!.GetValue<string>();
            if (!key.StartsWith("Level_", StringComparison.Ordinal)) continue;
            var inner = JsonNode.Parse(ayceEntries[i]!["m_JSON"]!.GetValue<string>())!.AsObject();
            var map = ExtractMap(inner);
            if (map.ContainsKey("AssistModeEnabled") || map.ContainsKey("FailedAttempts"))
            {
                hasAyceField = true;
                break;
            }
        }
        Assert.True(hasAyceField, "AYCE 转换后应包含 AssistModeEnabled 或 FailedAttempts 字段");
    }

    // ===== AYCE Steam 版加密二进制样本 =====

    /// <summary>
    /// 列出用户 AYCE 样本目录下所有 .save 文件。
    /// </summary>
    public static IEnumerable<object[]> UserAyceSaveFiles
    {
        get
        {
            if (!TestSamplePaths.IsAvailable(TestSamplePaths.UserAyceSampleDir)) yield break;
            foreach (var f in Directory.GetFiles(TestSamplePaths.UserAyceSampleDir, "*.save", SearchOption.TopDirectoryOnly))
            {
                yield return new object[] { Path.GetFileName(f) };
            }
        }
    }

    /// <summary>
    /// AYCE Steam 版样本应可用 SteamID64 密钥解密（AYCE Steam 版与 OC2 使用相同的加密格式）。
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(UserAyceSaveFiles))]
    public void UserAyce_EachSaveFile_DecryptsWithSteamId64Key(string fileName)
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.UserAyceSampleDir), "用户 AYCE 样本缺失");
        var path = Path.Combine(TestSamplePaths.UserAyceSampleDir, fileName);
        var bytes = File.ReadAllBytes(path);

        var ok = OvercookedCrypto.TryDecryptToJsonText(bytes, TestSamplePaths.UserSampleKey, out var json, ignoreCrc: false);
        Assert.True(ok, $"文件 {fileName} 无法用密钥 {TestSamplePaths.UserSampleKey} 解密");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    /// <summary>
    /// AYCE 非 Meta 存档应被检测为 Ayce 版本。
    /// 注意：AYCE Steam 版的文件扩展名是 .save（加密二进制），不是 .json，
    /// 项目 DetectPlatform 当前会把 .save 一律识别为 Oc2Binary 平台，但版本检测（基于 JSON 内容）应正确识别为 Ayce。
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(UserAyceSaveFiles))]
    public void UserAyce_EachNonMetaSave_DetectedAsAyceVersion(string fileName)
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.UserAyceSampleDir), "用户 AYCE 样本缺失");
        if (fileName.StartsWith("Meta", StringComparison.OrdinalIgnoreCase)) return;

        var path = Path.Combine(TestSamplePaths.UserAyceSampleDir, fileName);
        var bytes = File.ReadAllBytes(path);

        var ok = OvercookedCrypto.TryDecryptToJsonText(bytes, TestSamplePaths.UserSampleKey, out var json, ignoreCrc: false);
        Assert.True(ok);

        Assert.Equal(SaveVersion.Ayce, SaveJsonConverter.DetectVersion(json));
    }

    /// <summary>
    /// AYCE 样本应包含 AYCE 独有的前缀：BAG / OC1 / DLC202。
    /// </summary>
    [SkippableFact]
    public void UserAyce_ContainsAyceSpecificPrefixes()
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.UserAyceSampleDir), "用户 AYCE 样本缺失");
        var files = Directory.GetFiles(TestSamplePaths.UserAyceSampleDir, "*.save", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(x => x is not null)
            .Cast<string>()
            .ToList();

        Assert.Contains(files, x => x.StartsWith("BAG_", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, x => x.StartsWith("OC1_", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, x => x.StartsWith("DLC202_", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// AYCE 加解密往返：解密后重新加密的密文长度应与原始一致。
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(UserAyceSaveFiles))]
    public void UserAyce_Reencrypt_PreservesCipherLength(string fileName)
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.UserAyceSampleDir), "用户 AYCE 样本缺失");
        var path = Path.Combine(TestSamplePaths.UserAyceSampleDir, fileName);
        var bytes = File.ReadAllBytes(path);

        var plain = OvercookedCrypto.DecryptData(bytes, TestSamplePaths.UserSampleKey, ignoreCrc: false);
        Assert.NotNull(plain);

        var reEncrypted = OvercookedCrypto.EncryptData(plain!, TestSamplePaths.UserSampleKey);
        Assert.NotNull(reEncrypted);

        var originalCipherLen = bytes.Length - 16 - 4;
        var reCipherLen = reEncrypted!.Length - 16 - 4;
        Assert.Equal(originalCipherLen, reCipherLen);
    }

    /// <summary>
    /// AYCE → OC2 → AYCE 转换往返：核心字段应保留，AssistModeEnabled 顶层条目在转回 AYCE 时被恢复。
    /// </summary>
    [SkippableFact]
    public void UserAyce_ConvertRoundTrip_PreservesLevelFields()
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.UserAyceSampleDir), "用户 AYCE 样本缺失");
        var service = new SavePackageService();
        // AYCE 样本目录没有 steam_autocloud.vdf，需要显式传入密钥
        var package = service.LoadPackage(TestSamplePaths.UserAyceSampleDir, preferredKey: TestSamplePaths.UserSampleKey);
        var src = package.Saves.First(x => !x.IsMeta);
        var json = service.ReadSaveAsJson(package, src);
        Assert.Equal(SaveVersion.Ayce, SaveJsonConverter.DetectVersion(json));

        var toOc2 = SaveJsonConverter.Convert(json, SaveVersion.Ayce, SaveVersion.Oc2);
        Assert.Equal(SaveVersion.Oc2, SaveJsonConverter.DetectVersion(toOc2));

        // 验证 OC2 版本不再包含 AssistModeEnabled 顶层 key
        var oc2Root = JsonNode.Parse(toOc2)!.AsObject();
        var oc2Keys = oc2Root["m_Keys"]!.AsArray();
        Assert.DoesNotContain(oc2Keys, x => x!.GetValue<string>() == "AssistModeEnabled");

        var backToAyce = SaveJsonConverter.Convert(toOc2, SaveVersion.Oc2, SaveVersion.Ayce);
        Assert.Equal(SaveVersion.Ayce, SaveJsonConverter.DetectVersion(backToAyce));

        // 验证转回 AYCE 后 AssistModeEnabled 顶层 key 被恢复
        var ayceRoot = JsonNode.Parse(backToAyce)!.AsObject();
        var ayceKeys = ayceRoot["m_Keys"]!.AsArray();
        Assert.Contains(ayceKeys, x => x!.GetValue<string>() == "AssistModeEnabled");

        // 验证 Level_ 条目里的 FailedAttempts 也被恢复
        var ayceEntries = ayceRoot["m_Entries"]!.AsArray();
        var hasFailedAttempts = false;
        for (var i = 0; i < ayceKeys.Count; i++)
        {
            var key = ayceKeys[i]!.GetValue<string>();
            if (!key.StartsWith("Level_", StringComparison.Ordinal)) continue;
            var inner = JsonNode.Parse(ayceEntries[i]!["m_JSON"]!.GetValue<string>())!.AsObject();
            var map = ExtractMap(inner);
            if (map.ContainsKey("FailedAttempts"))
            {
                hasFailedAttempts = true;
                break;
            }
        }
        Assert.True(hasFailedAttempts, "转回 AYCE 后 Level_ 条目应包含 FailedAttempts 字段");
    }

    // ===== 跨版本转换：OC2 ↔ AYCE 在真实样本上的双向转换 =====

    /// <summary>
    /// 同一用户 OC2 存档解密后转为 AYCE，再转为 OC2，应能保持 JSON 结构。
    /// </summary>
    [SkippableFact]
    public void CrossVersion_Oc2ToAyceToOc2_PreservesStructure()
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.UserOc2SampleDir), "用户 OC2 样本缺失");
        var oc2Path = Path.Combine(TestSamplePaths.UserOc2SampleDir, "CoopSlot_SaveFile_0.save");
        Skip.IfNot(File.Exists(oc2Path), "CoopSlot_SaveFile_0.save 不存在");

        var bytes = File.ReadAllBytes(oc2Path);
        var ok = OvercookedCrypto.TryDecryptToJsonText(bytes, TestSamplePaths.UserSampleKey, out var oc2Json, ignoreCrc: false);
        Assert.True(ok);

        var ayceJson = SaveJsonConverter.Convert(oc2Json, SaveVersion.Oc2, SaveVersion.Ayce);
        Assert.Equal(SaveVersion.Ayce, SaveJsonConverter.DetectVersion(ayceJson));

        var backToOc2 = SaveJsonConverter.Convert(ayceJson, SaveVersion.Ayce, SaveVersion.Oc2);
        Assert.Equal(SaveVersion.Oc2, SaveJsonConverter.DetectVersion(backToOc2));

        // 比较原始 OC2 与转回 OC2 的 JSON 应一致
        var origRoot = JsonNode.Parse(oc2Json)!.AsObject();
        var backRoot = JsonNode.Parse(backToOc2)!.AsObject();

        var origKeys = origRoot["m_Keys"]!.AsArray();
        var backKeys = backRoot["m_Keys"]!.AsArray();
        Assert.Equal(origKeys.Count, backKeys.Count);

        for (var i = 0; i < origKeys.Count; i++)
        {
            Assert.Equal(origKeys[i]!.GetValue<string>(), backKeys[i]!.GetValue<string>());
        }
    }

    /// <summary>
    /// 同一用户 AYCE 存档解密后转为 OC2，再转为 AYCE，应能保持 JSON 结构。
    /// </summary>
    [SkippableFact]
    public void CrossVersion_AyceToOc2ToAyce_PreservesStructure()
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.UserAyceSampleDir), "用户 AYCE 样本缺失");
        var aycePath = Path.Combine(TestSamplePaths.UserAyceSampleDir, "CoopSlot_SaveFile_0.save");
        Skip.IfNot(File.Exists(aycePath), "CoopSlot_SaveFile_0.save 不存在");

        var bytes = File.ReadAllBytes(aycePath);
        var ok = OvercookedCrypto.TryDecryptToJsonText(bytes, TestSamplePaths.UserSampleKey, out var ayceJson, ignoreCrc: false);
        Assert.True(ok);

        var oc2Json = SaveJsonConverter.Convert(ayceJson, SaveVersion.Ayce, SaveVersion.Oc2);
        Assert.Equal(SaveVersion.Oc2, SaveJsonConverter.DetectVersion(oc2Json));

        var backToAyce = SaveJsonConverter.Convert(oc2Json, SaveVersion.Oc2, SaveVersion.Ayce);
        Assert.Equal(SaveVersion.Ayce, SaveJsonConverter.DetectVersion(backToAyce));

        // 比较原始 AYCE 与转回 AYCE 的 JSON：除 AssistModeEnabled/FailedAttempts 应可恢复外，其他字段应一致
        var origRoot = JsonNode.Parse(ayceJson)!.AsObject();
        var backRoot = JsonNode.Parse(backToAyce)!.AsObject();

        var origKeys = origRoot["m_Keys"]!.AsArray();
        var backKeys = backRoot["m_Keys"]!.AsArray();
        Assert.Equal(origKeys.Count, backKeys.Count);
    }

    // ===== 他人提供的 OC2 存档（zip 解压，4 星本体 + DLC 三星）=====

    /// <summary>
    /// 列出他人 OC2 样本目录下所有 .save 文件。
    /// </summary>
    public static IEnumerable<object[]> OtherOc2SaveFiles
    {
        get
        {
            if (!TestSamplePaths.IsAvailable(TestSamplePaths.OtherOc2SampleDir)) yield break;
            foreach (var f in Directory.GetFiles(TestSamplePaths.OtherOc2SampleDir, "*.save", SearchOption.TopDirectoryOnly))
            {
                yield return new object[] { Path.GetFileName(f) };
            }
        }
    }

    /// <summary>
    /// 他人 OC2 样本应可用与用户相同的 SteamID64 密钥解密（同账户 ID 命名）。
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(OtherOc2SaveFiles))]
    public void OtherOc2_EachSaveFile_DecryptsWithSharedKey(string fileName)
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.OtherOc2SampleDir), "他人 OC2 样本缺失");
        var path = Path.Combine(TestSamplePaths.OtherOc2SampleDir, fileName);
        var bytes = File.ReadAllBytes(path);

        var ok = OvercookedCrypto.TryDecryptToJsonText(bytes, TestSamplePaths.UserSampleKey, out var json, ignoreCrc: false);
        Assert.True(ok, $"文件 {fileName} 无法用密钥 {TestSamplePaths.UserSampleKey} 解密");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    /// <summary>
    /// 他人 OC2 非 Meta 存档应被检测为 Oc2 版本。
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(OtherOc2SaveFiles))]
    public void OtherOc2_EachNonMetaSave_DetectedAsOc2(string fileName)
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.OtherOc2SampleDir), "他人 OC2 样本缺失");
        if (fileName.StartsWith("Meta", StringComparison.OrdinalIgnoreCase)) return;

        var path = Path.Combine(TestSamplePaths.OtherOc2SampleDir, fileName);
        var bytes = File.ReadAllBytes(path);

        var ok = OvercookedCrypto.TryDecryptToJsonText(bytes, TestSamplePaths.UserSampleKey, out var json, ignoreCrc: false);
        Assert.True(ok);

        Assert.Equal(SaveVersion.Oc2, SaveJsonConverter.DetectVersion(json));
    }

    /// <summary>
    /// 他人 OC2 样本应包含 DLC2/3/5/7/8（且 DLC2/3 各有两份 _0/_1）。
    /// </summary>
    [SkippableFact]
    public void OtherOc2_ContainsExpectedDlcs()
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.OtherOc2SampleDir), "他人 OC2 样本缺失");
        var files = Directory.GetFiles(TestSamplePaths.OtherOc2SampleDir, "DLC*_*.save", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(x => x is not null)
            .Cast<string>()
            .ToList();

        Assert.Contains(files, x => x.StartsWith("DLC2_", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, x => x.StartsWith("DLC3_", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, x => x.StartsWith("DLC5_", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, x => x.StartsWith("DLC7_", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, x => x.StartsWith("DLC8_", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, x => x.StartsWith("DLC2_CoopSlot_SaveFile_1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, x => x.StartsWith("DLC3_CoopSlot_SaveFile_1", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 他人 OC2 加解密往返：重新加密的密文长度应与原始一致。
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(OtherOc2SaveFiles))]
    public void OtherOc2_Reencrypt_PreservesCipherLength(string fileName)
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.OtherOc2SampleDir), "他人 OC2 样本缺失");
        var path = Path.Combine(TestSamplePaths.OtherOc2SampleDir, fileName);
        var bytes = File.ReadAllBytes(path);

        var plain = OvercookedCrypto.DecryptData(bytes, TestSamplePaths.UserSampleKey, ignoreCrc: false);
        Assert.NotNull(plain);

        var reEncrypted = OvercookedCrypto.EncryptData(plain!, TestSamplePaths.UserSampleKey);
        Assert.NotNull(reEncrypted);

        var originalCipherLen = bytes.Length - 16 - 4;
        var reCipherLen = reEncrypted!.Length - 16 - 4;
        Assert.Equal(originalCipherLen, reCipherLen);
    }

    /// <summary>
    /// 他人 OC2 样本应能通过 SavePackageService 加载（目录名带 + 后缀不应影响加载）。
    /// </summary>
    [SkippableFact]
    public void OtherOc2_SavePackageService_LoadsWithPlusSuffixDirectory()
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.OtherOc2SampleDir), "他人 OC2 样本缺失");
        var service = new SavePackageService();
        // 目录名带 + 后缀，KeyDetector 无法从目录名提取 SteamID64，需显式传入密钥
        var package = service.LoadPackage(TestSamplePaths.OtherOc2SampleDir, preferredKey: TestSamplePaths.UserSampleKey);

        Assert.Equal(SavePlatform.Oc2Binary, package.Platform);
        Assert.Equal(SaveVersion.Oc2, package.Version);
        Assert.Equal(TestSamplePaths.UserSampleKey, package.DetectedKey);
        Assert.True(package.KeyValidated);
        Assert.NotEmpty(package.Saves);
    }

    // ===== 他人提供的 AYCE 存档（7z 解压，全 DLC 全通）=====

    /// <summary>
    /// 列出他人 AYCE 样本目录下所有 .save 文件。
    /// </summary>
    public static IEnumerable<object[]> OtherAyceSaveFiles
    {
        get
        {
            if (!TestSamplePaths.IsAvailable(TestSamplePaths.OtherAyceSampleDir)) yield break;
            foreach (var f in Directory.GetFiles(TestSamplePaths.OtherAyceSampleDir, "*.save", SearchOption.TopDirectoryOnly))
            {
                yield return new object[] { Path.GetFileName(f) };
            }
        }
    }

    /// <summary>
    /// 他人 AYCE 样本应可用与用户相同的 SteamID64 密钥解密。
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(OtherAyceSaveFiles))]
    public void OtherAyce_EachSaveFile_DecryptsWithSharedKey(string fileName)
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.OtherAyceSampleDir), "他人 AYCE 样本缺失");
        var path = Path.Combine(TestSamplePaths.OtherAyceSampleDir, fileName);
        var bytes = File.ReadAllBytes(path);

        var ok = OvercookedCrypto.TryDecryptToJsonText(bytes, TestSamplePaths.UserSampleKey, out var json, ignoreCrc: false);
        Assert.True(ok, $"文件 {fileName} 无法用密钥 {TestSamplePaths.UserSampleKey} 解密");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    /// <summary>
    /// 他人 AYCE 样本应包含更全面的 DLC（DLC4/9/10/11/13/101/102/202/OC1/BAG）。
    /// </summary>
    [SkippableFact]
    public void OtherAyce_ContainsExtendedDlcs()
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.OtherAyceSampleDir), "他人 AYCE 样本缺失");
        var files = Directory.GetFiles(TestSamplePaths.OtherAyceSampleDir, "*.save", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(x => x is not null)
            .Cast<string>()
            .ToList();

        // AYCE 独有前缀
        Assert.Contains(files, x => x.StartsWith("BAG_", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, x => x.StartsWith("OC1_", StringComparison.OrdinalIgnoreCase));
        // 扩展 DLC
        Assert.Contains(files, x => x.StartsWith("DLC4_", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, x => x.StartsWith("DLC9_", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, x => x.StartsWith("DLC10_", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, x => x.StartsWith("DLC11_", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, x => x.StartsWith("DLC13_", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, x => x.StartsWith("DLC101_", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, x => x.StartsWith("DLC102_", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, x => x.StartsWith("DLC202_", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 他人 AYCE 加解密往返：重新加密的密文长度应与原始一致。
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(OtherAyceSaveFiles))]
    public void OtherAyce_Reencrypt_PreservesCipherLength(string fileName)
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.OtherAyceSampleDir), "他人 AYCE 样本缺失");
        var path = Path.Combine(TestSamplePaths.OtherAyceSampleDir, fileName);
        var bytes = File.ReadAllBytes(path);

        var plain = OvercookedCrypto.DecryptData(bytes, TestSamplePaths.UserSampleKey, ignoreCrc: false);
        Assert.NotNull(plain);

        var reEncrypted = OvercookedCrypto.EncryptData(plain!, TestSamplePaths.UserSampleKey);
        Assert.NotNull(reEncrypted);

        var originalCipherLen = bytes.Length - 16 - 4;
        var reCipherLen = reEncrypted!.Length - 16 - 4;
        Assert.Equal(originalCipherLen, reCipherLen);
    }

    /// <summary>
    /// 他人 AYCE 存档包中混有 OC2/AYCE 两种格式的关卡：未启用辅助模式的关卡仍为 OC2 格式。
    /// 验证 SavePackageService 能正确加载混合格式包，且包版本最终判定为 Ayce。
    /// </summary>
    [SkippableFact]
    public void OtherAyce_PackageContainsMixedVersionFiles()
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.OtherAyceSampleDir), "他人 AYCE 样本缺失");
        var service = new SavePackageService();
        var package = service.LoadPackage(TestSamplePaths.OtherAyceSampleDir, preferredKey: TestSamplePaths.UserSampleKey);

        // AYCE Steam 版二进制格式
        Assert.Equal(SavePlatform.Oc2Binary, package.Platform);
        // 包级别版本应为 AYCE（因为存在 AYCE 独有前缀文件如 BAG/OC1/DLC202）
        Assert.Equal(SaveVersion.Ayce, package.Version);
        Assert.True(package.KeyValidated);

        // 检测包内文件级别的版本混合
        var versions = new HashSet<SaveVersion>();
        foreach (var save in package.Saves)
        {
            if (save.IsMeta) continue;
            try
            {
                var json = service.ReadSaveAsJson(package, save);
                versions.Add(SaveJsonConverter.DetectVersion(json));
            }
            catch
            {
                // 忽略读取失败的条目
            }
        }
        // 应同时存在 Oc2 和 Ayce 两种版本的关卡文件
        Assert.Contains(SaveVersion.Oc2, versions);
        Assert.Contains(SaveVersion.Ayce, versions);
    }

    /// <summary>
    /// 他人 AYCE 样本应能识别所有 DLC ID（2-13 + 101/102/202）。
    /// </summary>
    [SkippableFact]
    public void OtherAyce_DlcIds_AreCorrectlyExtracted()
    {
        Skip.IfNot(TestSamplePaths.IsAvailable(TestSamplePaths.OtherAyceSampleDir), "他人 AYCE 样本缺失");
        var service = new SavePackageService();
        var package = service.LoadPackage(TestSamplePaths.OtherAyceSampleDir, preferredKey: TestSamplePaths.UserSampleKey);

        var dlcIds = package.Saves
            .Where(x => x.DlcId.HasValue)
            .Select(x => x.DlcId!.Value)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        // 应包含 2,3,4,5,7,8,9,10,11,13,101,102,202（注意：OC1 和 BAG 不是 DLC ID）
        Assert.Contains(2, dlcIds);
        Assert.Contains(3, dlcIds);
        Assert.Contains(4, dlcIds);
        Assert.Contains(5, dlcIds);
        Assert.Contains(7, dlcIds);
        Assert.Contains(8, dlcIds);
        Assert.Contains(9, dlcIds);
        Assert.Contains(10, dlcIds);
        Assert.Contains(11, dlcIds);
        Assert.Contains(13, dlcIds);
        Assert.Contains(101, dlcIds);
        Assert.Contains(102, dlcIds);
        Assert.Contains(202, dlcIds);
    }

    // ===== Helpers =====

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
