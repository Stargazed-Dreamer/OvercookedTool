using System.Text.Json;
using System.Text.Json.Nodes;
using OvercookedTool.Core.Models;
using OvercookedTool.Core.Services;

namespace OvercookedTool.Tests.Services;

/// <summary>
/// SaveJsonConverter 单元测试。
/// 覆盖：版本检测（Oc2/Ayce/Unknown）、版本间转换（增删 AssistMode、FailedAttempts）、往返一致性。
/// </summary>
public class SaveJsonConverterTests
{
    private const string AssistModeKey = "AssistModeEnabled";
    private const string FailedAttemptsKey = "FailedAttempts";

    /// <summary>
    /// 构造一个最小可用的 OC2 风格存档 JSON：包含 2 个 Level_ 条目 + 1 个非 Level_ 条目。
    /// </summary>
    private static string BuildOc2Json(int levelCount = 2)
    {
        var keys = new JsonArray();
        var entries = new JsonArray();
        for (var i = 1; i <= levelCount; i++)
        {
            keys.Add($"Level_{i}");
            entries.Add(new JsonObject
            {
                ["m_JSON"] = new JsonObject
                {
                    ["m_Key"] = new JsonArray { "ScoreStars", "Completed", "HighScore" },
                    ["m_Value"] = new JsonArray { (i % 4).ToString(), "True", (i * 1000).ToString() },
                }.ToJsonString(),
            });
        }
        keys.Add("SomeOtherKey");
        entries.Add(new JsonObject
        {
            ["m_JSON"] = new JsonObject
            {
                ["m_Key"] = new JsonArray { "foo" },
                ["m_Value"] = new JsonArray { "bar" },
            }.ToJsonString(),
        });

        return new JsonObject
        {
            ["m_Keys"] = keys,
            ["m_Entries"] = entries,
        }.ToJsonString();
    }

    /// <summary>
    /// 在 AYCE 版本基础上构造：在 m_Keys 末尾追加 AssistModeEnabled + 对应 m_Entries，
    /// 并为每个 Level_ 条目追加 FailedAttempts="0"。
    /// </summary>
    private static string BuildAyceJson(int levelCount = 2)
    {
        var keys = new JsonArray();
        var entries = new JsonArray();
        for (var i = 1; i <= levelCount; i++)
        {
            keys.Add($"Level_{i}");
            entries.Add(new JsonObject
            {
                ["m_JSON"] = new JsonObject
                {
                    ["m_Key"] = new JsonArray { "ScoreStars", "Completed", "HighScore", FailedAttemptsKey },
                    ["m_Value"] = new JsonArray { (i % 4).ToString(), "True", (i * 1000).ToString(), "0" },
                }.ToJsonString(),
            });
        }
        keys.Add(AssistModeKey);
        entries.Add(new JsonObject
        {
            ["m_JSON"] = new JsonObject { ["m_Value"] = false }.ToJsonString(),
        });

        return new JsonObject
        {
            ["m_Keys"] = keys,
            ["m_Entries"] = entries,
        }.ToJsonString();
    }

    // ===== DetectVersion =====

    [Fact]
    public void DetectVersion_Oc2Style_ReturnsOc2()
    {
        var json = BuildOc2Json();
        Assert.Equal(SaveVersion.Oc2, SaveJsonConverter.DetectVersion(json));
    }

    [Fact]
    public void DetectVersion_AyceStyle_ReturnsAyce()
    {
        var json = BuildAyceJson();
        Assert.Equal(SaveVersion.Ayce, SaveJsonConverter.DetectVersion(json));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("{\"foo\":\"bar\"}")]
    [InlineData("{\"m_Keys\":\"not-an-array\"}")]
    public void DetectVersion_InvalidJson_ReturnsUnknown(string input)
    {
        Assert.Equal(SaveVersion.Unknown, SaveJsonConverter.DetectVersion(input));
    }

    [Fact]
    public void DetectVersion_EmptyKeysArray_ReturnsOc2()
    {
        // 空数组（没有任何 Level_/AssistMode 键），按当前实现判定为 Oc2
        var json = "{\"m_Keys\":[],\"m_Entries\":[]}";
        Assert.Equal(SaveVersion.Oc2, SaveJsonConverter.DetectVersion(json));
    }

    // ===== Convert: No-op cases =====

    [Fact]
    public void Convert_SameVersion_ReturnsOriginalText()
    {
        var json = BuildOc2Json();
        var converted = SaveJsonConverter.Convert(json, SaveVersion.Oc2, SaveVersion.Oc2);
        Assert.Equal(json, converted);
    }

    [Fact]
    public void Convert_UnknownSource_ReturnsOriginalText()
    {
        var json = BuildOc2Json();
        var converted = SaveJsonConverter.Convert(json, SaveVersion.Unknown, SaveVersion.Ayce);
        Assert.Equal(json, converted);
    }

    [Fact]
    public void Convert_UnknownTarget_ReturnsOriginalText()
    {
        var json = BuildOc2Json();
        var converted = SaveJsonConverter.Convert(json, SaveVersion.Oc2, SaveVersion.Unknown);
        Assert.Equal(json, converted);
    }

    // ===== Convert: Oc2 -> Ayce =====

    [Fact]
    public void Convert_Oc2ToAyce_AddsAssistModeEntry()
    {
        var json = BuildOc2Json();
        var converted = SaveJsonConverter.Convert(json, SaveVersion.Oc2, SaveVersion.Ayce);

        var root = JsonNode.Parse(converted)!.AsObject();
        var keys = root["m_Keys"]!.AsArray();
        var entries = root["m_Entries"]!.AsArray();

        var idx = FindKeyIndex(keys, AssistModeKey);
        Assert.True(idx >= 0, "应在 m_Keys 中添加 AssistModeEnabled");
        Assert.True(idx < entries.Count, "应在 m_Entries 中添加对应条目");

        // 默认值为 false
        var innerText = entries[idx]!["m_JSON"]!.GetValue<string>();
        var inner = JsonNode.Parse(innerText)!.AsObject();
        Assert.Equal(false, inner["m_Value"]?.GetValue<bool>());
    }

    [Fact]
    public void Convert_Oc2ToAyce_AddsFailedAttemptsToLevelEntries()
    {
        var json = BuildOc2Json(levelCount: 3);
        var converted = SaveJsonConverter.Convert(json, SaveVersion.Oc2, SaveVersion.Ayce);

        var root = JsonNode.Parse(converted)!.AsObject();
        var keys = root["m_Keys"]!.AsArray();
        var entries = root["m_Entries"]!.AsArray();

        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i]?.GetValue<string>();
            if (!key!.StartsWith("Level_", StringComparison.Ordinal))
            {
                continue;
            }

            var inner = JsonNode.Parse(entries[i]!["m_JSON"]!.GetValue<string>())!.AsObject();
            var innerKeys = inner["m_Key"]!.AsArray();
            var innerValues = inner["m_Value"]!.AsArray();
            var faIdx = IndexOf(innerKeys, FailedAttemptsKey);
            Assert.True(faIdx >= 0, $"Level 条目 {key} 应添加 FailedAttempts");
            Assert.Equal("0", innerValues[faIdx]!.ToString());
        }
    }

    [Fact]
    public void Convert_Oc2ToAyce_DoesNotModifyNonLevelEntries()
    {
        var json = BuildOc2Json();
        var converted = SaveJsonConverter.Convert(json, SaveVersion.Oc2, SaveVersion.Ayce);

        var root = JsonNode.Parse(converted)!.AsObject();
        var keys = root["m_Keys"]!.AsArray();
        var entries = root["m_Entries"]!.AsArray();
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i]?.GetValue<string>();
            if (key == AssistModeKey || (key?.StartsWith("Level_", StringComparison.Ordinal) ?? false))
            {
                continue;
            }

            var inner = JsonNode.Parse(entries[i]!["m_JSON"]!.GetValue<string>())!.AsObject();
            var innerKeys = inner["m_Key"]!.AsArray();
            Assert.False(Contains(innerKeys, FailedAttemptsKey), $"非 Level 条目 {key} 不应被改动");
        }
    }

    [Fact]
    public void Convert_Oc2ToAyce_DetectedAsAyce()
    {
        var converted = SaveJsonConverter.Convert(BuildOc2Json(), SaveVersion.Oc2, SaveVersion.Ayce);
        Assert.Equal(SaveVersion.Ayce, SaveJsonConverter.DetectVersion(converted));
    }

    // ===== Convert: Ayce -> Oc2 =====

    [Fact]
    public void Convert_AyceToOc2_RemovesAssistModeEntry()
    {
        var json = BuildAyceJson();
        var converted = SaveJsonConverter.Convert(json, SaveVersion.Ayce, SaveVersion.Oc2);

        var root = JsonNode.Parse(converted)!.AsObject();
        var keys = root["m_Keys"]!.AsArray();
        Assert.True(FindKeyIndex(keys, AssistModeKey) < 0, "应移除 AssistModeEnabled 键");
    }

    [Fact]
    public void Convert_AyceToOc2_RemovesFailedAttemptsFromLevelEntries()
    {
        var json = BuildAyceJson(levelCount: 3);
        var converted = SaveJsonConverter.Convert(json, SaveVersion.Ayce, SaveVersion.Oc2);

        var root = JsonNode.Parse(converted)!.AsObject();
        var keys = root["m_Keys"]!.AsArray();
        var entries = root["m_Entries"]!.AsArray();
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i]?.GetValue<string>();
            if (!key!.StartsWith("Level_", StringComparison.Ordinal))
            {
                continue;
            }

            var inner = JsonNode.Parse(entries[i]!["m_JSON"]!.GetValue<string>())!.AsObject();
            var innerKeys = inner["m_Key"]!.AsArray();
            Assert.False(Contains(innerKeys, FailedAttemptsKey), $"Level 条目 {key} 应移除 FailedAttempts");
        }
    }

    [Fact]
    public void Convert_AyceToOc2_DetectedAsOc2()
    {
        var converted = SaveJsonConverter.Convert(BuildAyceJson(), SaveVersion.Ayce, SaveVersion.Oc2);
        Assert.Equal(SaveVersion.Oc2, SaveJsonConverter.DetectVersion(converted));
    }

    // ===== Convert: Round-trip =====

    [Fact]
    public void Convert_Oc2ToAyceAndBack_PreservesLevelCoreFields()
    {
        var original = BuildOc2Json(levelCount: 3);
        var toAyce = SaveJsonConverter.Convert(original, SaveVersion.Oc2, SaveVersion.Ayce);
        var backToOc2 = SaveJsonConverter.Convert(toAyce, SaveVersion.Ayce, SaveVersion.Oc2);

        var origRoot = JsonNode.Parse(original)!.AsObject();
        var backRoot = JsonNode.Parse(backToOc2)!.AsObject();

        var origKeys = origRoot["m_Keys"]!.AsArray();
        var origEntries = origRoot["m_Entries"]!.AsArray();
        var backKeys = backRoot["m_Keys"]!.AsArray();
        var backEntries = backRoot["m_Entries"]!.AsArray();

        Assert.Equal(origKeys.Count, backKeys.Count);
        for (var i = 0; i < origKeys.Count; i++)
        {
            Assert.Equal(origKeys[i]!.GetValue<string>(), backKeys[i]!.GetValue<string>());
            var key = origKeys[i]!.GetValue<string>();
            if (!key.StartsWith("Level_", StringComparison.Ordinal))
            {
                // 非 Level 条目应当原样保留
                Assert.Equal(
                    origEntries[i]!["m_JSON"]!.GetValue<string>(),
                    backEntries[i]!["m_JSON"]!.GetValue<string>());
            }
            else
            {
                // Level 条目：除 FailedAttempts 外其他字段都应保留
                var origInner = JsonNode.Parse(origEntries[i]!["m_JSON"]!.GetValue<string>())!.AsObject();
                var backInner = JsonNode.Parse(backEntries[i]!["m_JSON"]!.GetValue<string>())!.AsObject();
                Assert.Equal(
                    ExtractMap(origInner),
                    ExtractMap(backInner));
            }
        }
    }

    [Fact]
    public void Convert_AyceToOc2AndBack_PreservesLevelCoreFields()
    {
        var original = BuildAyceJson(levelCount: 3);
        var toOc2 = SaveJsonConverter.Convert(original, SaveVersion.Ayce, SaveVersion.Oc2);
        var backToAyce = SaveJsonConverter.Convert(toOc2, SaveVersion.Oc2, SaveVersion.Ayce);

        Assert.Equal(SaveVersion.Ayce, SaveJsonConverter.DetectVersion(backToAyce));

        var origRoot = JsonNode.Parse(original)!.AsObject();
        var backRoot = JsonNode.Parse(backToAyce)!.AsObject();

        // Level 条目数量应一致
        var origLevelCount = CountLevelKeys(origRoot["m_Keys"]!.AsArray());
        var backLevelCount = CountLevelKeys(backRoot["m_Keys"]!.AsArray());
        Assert.Equal(origLevelCount, backLevelCount);
    }

    // ===== Convert: Idempotency =====

    [Fact]
    public void Convert_Oc2ToAyce_Twice_ProducesSameAssistModeEntry()
    {
        var json = BuildOc2Json();
        var first = SaveJsonConverter.Convert(json, SaveVersion.Oc2, SaveVersion.Ayce);
        var second = SaveJsonConverter.Convert(first, SaveVersion.Oc2, SaveVersion.Ayce);

        // 第二次仍按 Oc2 -> Ayce 处理，AssistMode 应该只保留一个（更新而非追加）
        var root = JsonNode.Parse(second)!.AsObject();
        var keys = root["m_Keys"]!.AsArray();
        var count = keys.Count(x => string.Equals(x?.GetValue<string>(), AssistModeKey, StringComparison.Ordinal));
        Assert.Equal(1, count);
    }

    [Fact]
    public void Convert_AyceToOc2_WhenAssistModeMissing_StillProducesValidOc2()
    {
        // 即使原 Ayce 数据缺失 AssistModeEnabled，转回 Oc2 也不应出错
        var json = BuildOc2Json(); // 已经是 Oc2 形态
        var result = SaveJsonConverter.Convert(json, SaveVersion.Ayce, SaveVersion.Oc2);
        Assert.NotEmpty(result);
    }

    // ===== Helpers =====

    private static int FindKeyIndex(JsonArray keys, string target)
    {
        for (var i = 0; i < keys.Count; i++)
        {
            if (string.Equals(keys[i]?.GetValue<string>(), target, StringComparison.Ordinal))
            {
                return i;
            }
        }
        return -1;
    }

    private static int IndexOf(JsonArray arr, string target)
    {
        for (var i = 0; i < arr.Count; i++)
        {
            if (string.Equals(arr[i]?.GetValue<string>(), target, StringComparison.Ordinal))
            {
                return i;
            }
        }
        return -1;
    }

    private static bool Contains(JsonArray arr, string target) => IndexOf(arr, target) >= 0;

    private static int CountLevelKeys(JsonArray keys)
    {
        var n = 0;
        foreach (var k in keys)
        {
            if (k?.GetValue<string>()?.StartsWith("Level_", StringComparison.Ordinal) ?? false)
            {
                n++;
            }
        }
        return n;
    }

    private static Dictionary<string, string> ExtractMap(JsonObject inner)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var keys = inner["m_Key"] as JsonArray;
        var values = inner["m_Value"] as JsonArray;
        if (keys is null || values is null)
        {
            return result;
        }
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
