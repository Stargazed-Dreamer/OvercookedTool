using System.Text.Json;
using System.Text.Json.Nodes;
using OvercookedTool.Core.Models;

namespace OvercookedTool.Core.Services;

/// <summary>
/// 存档JSON转换器类，用于处理不同游戏版本之间存档数据的格式转换。
/// </summary>
public static class SaveJsonConverter
{
    // 存储尝试次数的键名常量
    private const string FailedAttemptsKey = "FailedAttempts";
    // 存储辅助模式启用状态的键名常量
    private const string AssistModeEnabledKey = "AssistModeEnabled";

    /// <summary>
    /// 检测存档JSON数据的版本。
    /// </summary>
    /// <param name="jsonText">存档JSON文本</param>
    /// <returns>检测到的存档版本枚举值</returns>
    public static SaveVersion DetectVersion(string jsonText)
    {
        try
        {
            // 解析JSON文本为JsonNode对象
            var root = JsonNode.Parse(jsonText)?.AsObject();
            if (root is null)
            {
                // 如果根节点为空，返回未知版本
                return SaveVersion.Unknown;
            }

            // 获取键数组
            var keys = root["m_Keys"]?.AsArray();
            if (keys is null)
            {
                // 如果键数组为空，返回未知版本
                return SaveVersion.Unknown;
            }

            // 检查是否存在辅助模式键，用于区分版本
            var hasAssistMode = keys.Any(
                x => string.Equals(
                    x?.GetValue<string>(),
                    AssistModeEnabledKey,
                    StringComparison.Ordinal));

            // 根据辅助模式键是否存在判断版本：存在则为Ayce版本，否则为Oc2版本
            return hasAssistMode ? SaveVersion.Ayce : SaveVersion.Oc2;
        }
        catch
        {
            // 解析失败时返回未知版本
            return SaveVersion.Unknown;
        }
    }

    /// <summary>
    /// 将存档数据从源版本转换为目标版本。
    /// </summary>
    /// <param name="jsonText">原始存档JSON文本</param>
    /// <param name="sourceVersion">源版本</param>
    /// <param name="targetVersion">目标版本</param>
    /// <returns>转换后的存档JSON文本</returns>
    public static string Convert(string jsonText, SaveVersion sourceVersion, SaveVersion targetVersion)
    {
        // 如果源版本或目标版本未知，或者两者相同，则直接返回原始文本
        if (sourceVersion == SaveVersion.Unknown || targetVersion == SaveVersion.Unknown || sourceVersion == targetVersion)
        {
            return jsonText;
        }

        // 解析JSON文本，如果解析失败则抛出异常
        var root = JsonNode.Parse(jsonText)?.AsObject()
                   ?? throw new InvalidOperationException("Save JSON is invalid.");

        // 获取键数组，如果不存在则抛出异常
        var keys = root["m_Keys"]?.AsArray()
                   ?? throw new InvalidOperationException("Save JSON missing m_Keys.");
        // 获取条目数组，如果不存在则抛出异常
        var entries = root["m_Entries"]?.AsArray()
                      ?? throw new InvalidOperationException("Save JSON missing m_Entries.");

        // 确定要处理的条目数量（取键数组和条目数组长度的最小值）
        var count = Math.Min(keys.Count, entries.Count);
        for (var i = 0; i < count; i++)
        {
            // 获取当前键值，如果为空则使用空字符串
            var key = keys[i]?.GetValue<string>() ?? string.Empty;
            // 只处理以"Level_"开头的关卡数据
            if (!key.StartsWith("Level_", StringComparison.Ordinal))
            {
                continue;
            }

            // 解析内部条目对象
            var inner = ParseInnerEntry(entries[i]);
            if (inner is null)
            {
                continue;
            }

            // 获取内部键值对映射，如果获取失败或映射为空则跳过
            if (!TryGetInnerMap(inner, out var map) || map.Count == 0)
            {
                continue;
            }

            // 根据版本转换方向执行特定的数据转换
            if (sourceVersion == SaveVersion.Oc2 && targetVersion == SaveVersion.Ayce)
            {
                // 从Oc2转换到Ayce：添加失败尝试次数字段并设为"0"
                map[FailedAttemptsKey] = "0";
            }
            else if (sourceVersion == SaveVersion.Ayce && targetVersion == SaveVersion.Oc2)
            {
                // 从Ayce转换到Oc2：移除失败尝试次数字段
                map.Remove(FailedAttemptsKey);
            }

            // 更新内部映射并写回条目
            SetInnerMap(inner, map);
            WriteInnerEntry(entries[i], inner);
        }

        // 处理辅助模式相关条目的转换
        if (sourceVersion == SaveVersion.Oc2 && targetVersion == SaveVersion.Ayce)
        {
            // 从Oc2转换到Ayce：添加辅助模式条目
            AddAssistModeEntry(keys, entries);
        }
        else if (sourceVersion == SaveVersion.Ayce && targetVersion == SaveVersion.Oc2)
        {
            // 从Ayce转换到Oc2：移除辅助模式条目
            RemoveAssistModeEntry(keys, entries);
        }

        // 将修改后的JSON对象序列化为字符串并返回
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// 向存档数据中添加辅助模式启用状态的条目。
    /// </summary>
    /// <param name="keys">键数组</param>
    /// <param name="entries">条目数组</param>
    private static void AddAssistModeEntry(JsonArray keys, JsonArray entries)
    {
        // 查找已存在的辅助模式键的索引
        var existingIndex = FindKeyIndex(keys, AssistModeEnabledKey);
        // 创建新的内部对象，设置默认值为false
        var newInner = new JsonObject { ["m_Value"] = false };

        // 如果找到已存在的条目且索引在有效范围内，则更新该条目
        if (existingIndex >= 0 && existingIndex < entries.Count)
        {
            WriteInnerEntry(entries[existingIndex], newInner);
            return;
        }

        // 如果不存在，则添加新的键和条目
        keys.Add(AssistModeEnabledKey);
        entries.Add(new JsonObject
        {
            ["m_JSON"] = newInner.ToJsonString(new JsonSerializerOptions { WriteIndented = false }),
        });
    }

    /// <summary>
    /// 从存档数据中移除辅助模式启用状态的条目。
    /// </summary>
    /// <param name="keys">键数组</param>
    /// <param name="entries">条目数组</param>
    private static void RemoveAssistModeEntry(JsonArray keys, JsonArray entries)
    {
        // 查找辅助模式键的索引
        var index = FindKeyIndex(keys, AssistModeEnabledKey);
        // 如果未找到则直接返回
        if (index < 0)
        {
            return;
        }

        // 从键数组中移除该键
        keys.RemoveAt(index);
        // 如果索引在条目数组有效范围内，也移除对应的条目
        if (index < entries.Count)
        {
            entries.RemoveAt(index);
        }
    }

    /// <summary>
    /// 在键数组中查找指定键的索引。
    /// </summary>
    /// <param name="keys">键数组</param>
    /// <param name="targetKey">目标键名</param>
    /// <returns>找到的索引，未找到则返回-1</returns>
    private static int FindKeyIndex(JsonArray keys, string targetKey)
    {
        // 遍历键数组查找目标键
        for (var i = 0; i < keys.Count; i++)
        {
            var value = keys[i]?.GetValue<string>();
            if (string.Equals(value, targetKey, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 解析内部条目节点为JsonObject。
    /// </summary>
    /// <param name="entryNode">条目节点</param>
    /// <returns>解析后的内部JsonObject，解析失败则返回null</returns>
    private static JsonObject? ParseInnerEntry(JsonNode? entryNode)
    {
        // 尝试将节点转换为JsonObject
        var entryObj = entryNode as JsonObject;
        // 获取m_JSON字段的字符串值
        var jsonText = entryObj?["m_JSON"]?.GetValue<string>();
        // 如果文本为空或空白，则返回null
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            return null;
        }

        // 解析内部JSON字符串为JsonObject
        return JsonNode.Parse(jsonText)?.AsObject();
    }

    /// <summary>
    /// 将内部JsonObject写入条目节点。
    /// </summary>
    /// <param name="entryNode">条目节点</param>
    /// <param name="inner">要写入的内部JsonObject</param>
    private static void WriteInnerEntry(JsonNode? entryNode, JsonObject inner)
    {
        // 检查节点是否为JsonObject
        if (entryNode is not JsonObject obj)
        {
            return;
        }

        // 将内部对象序列化为字符串并写入m_JSON字段
        obj["m_JSON"] = inner.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// 从内部JsonObject中获取键值对映射。
    /// </summary>
    /// <param name="inner">内部JsonObject</param>
    /// <param name="map">输出的键值对字典</param>
    /// <returns>是否成功获取映射</returns>
    private static bool TryGetInnerMap(JsonObject inner, out Dictionary<string, JsonNode?> map)
    {
        // 初始化字典，使用序号比较器
        map = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        // 获取键数组和值数组
        var keyArray = inner["m_Key"] as JsonArray;
        var valueArray = inner["m_Value"] as JsonArray;
        // 如果任一数组为空，返回false
        if (keyArray is null || valueArray is null)
        {
            return false;
        }

        // 取两个数组长度的最小值作为处理数量
        var count = Math.Min(keyArray.Count, valueArray.Count);
        for (var i = 0; i < count; i++)
        {
            // 获取键名，如果为空或null则跳过
            var key = keyArray[i]?.GetValue<string>();
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            // 将键值对添加到字典，值使用深拷贝以避免引用问题
            map[key] = valueArray[i]?.DeepClone();
        }

        return true;
    }

    /// <summary>
    /// 将键值对映射设置到内部JsonObject中。
    /// </summary>
    /// <param name="inner">内部JsonObject</param>
    /// <param name="map">要设置的键值对字典</param>
    private static void SetInnerMap(JsonObject inner, Dictionary<string, JsonNode?> map)
    {
        // 创建新的键数组和值数组
        var keyArray = new JsonArray();
        var valueArray = new JsonArray();

        // 遍历字典，将键和值（深拷贝）添加到数组中
        foreach (var pair in map)
        {
            keyArray.Add(pair.Key);
            valueArray.Add(pair.Value?.DeepClone());
        }

        // 更新内部对象的键数组和值数组
        inner["m_Key"] = keyArray;
        inner["m_Value"] = valueArray;
    }
}

