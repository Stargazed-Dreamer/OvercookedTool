using System.Text.Json;
using System.Text.Json.Nodes;
using OvercookedTool.Core.Models;

namespace OvercookedTool.Core.Services;

public static class SaveJsonConverter
{
    private const string FailedAttemptsKey = "FailedAttempts";
    private const string AssistModeEnabledKey = "AssistModeEnabled";

    public static SaveVersion DetectVersion(string jsonText)
    {
        try
        {
            var root = JsonNode.Parse(jsonText)?.AsObject();
            if (root is null)
            {
                return SaveVersion.Unknown;
            }

            var keys = root["m_Keys"]?.AsArray();
            if (keys is null)
            {
                return SaveVersion.Unknown;
            }

            var hasAssistMode = keys.Any(
                x => string.Equals(
                    x?.GetValue<string>(),
                    AssistModeEnabledKey,
                    StringComparison.Ordinal));

            return hasAssistMode ? SaveVersion.Ayce : SaveVersion.Oc2;
        }
        catch
        {
            return SaveVersion.Unknown;
        }
    }

    public static string Convert(string jsonText, SaveVersion sourceVersion, SaveVersion targetVersion)
    {
        if (sourceVersion == SaveVersion.Unknown || targetVersion == SaveVersion.Unknown || sourceVersion == targetVersion)
        {
            return jsonText;
        }

        var root = JsonNode.Parse(jsonText)?.AsObject()
                   ?? throw new InvalidOperationException("Save JSON is invalid.");

        var keys = root["m_Keys"]?.AsArray()
                   ?? throw new InvalidOperationException("Save JSON missing m_Keys.");
        var entries = root["m_Entries"]?.AsArray()
                      ?? throw new InvalidOperationException("Save JSON missing m_Entries.");

        var count = Math.Min(keys.Count, entries.Count);
        for (var i = 0; i < count; i++)
        {
            var key = keys[i]?.GetValue<string>() ?? string.Empty;
            if (!key.StartsWith("Level_", StringComparison.Ordinal))
            {
                continue;
            }

            var inner = ParseInnerEntry(entries[i]);
            if (inner is null)
            {
                continue;
            }

            if (!TryGetInnerMap(inner, out var map) || map.Count == 0)
            {
                continue;
            }

            if (sourceVersion == SaveVersion.Oc2 && targetVersion == SaveVersion.Ayce)
            {
                map[FailedAttemptsKey] = "0";
            }
            else if (sourceVersion == SaveVersion.Ayce && targetVersion == SaveVersion.Oc2)
            {
                map.Remove(FailedAttemptsKey);
            }

            SetInnerMap(inner, map);
            WriteInnerEntry(entries[i], inner);
        }

        if (sourceVersion == SaveVersion.Oc2 && targetVersion == SaveVersion.Ayce)
        {
            AddAssistModeEntry(keys, entries);
        }
        else if (sourceVersion == SaveVersion.Ayce && targetVersion == SaveVersion.Oc2)
        {
            RemoveAssistModeEntry(keys, entries);
        }

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static void AddAssistModeEntry(JsonArray keys, JsonArray entries)
    {
        var existingIndex = FindKeyIndex(keys, AssistModeEnabledKey);
        var newInner = new JsonObject { ["m_Value"] = false };

        if (existingIndex >= 0 && existingIndex < entries.Count)
        {
            WriteInnerEntry(entries[existingIndex], newInner);
            return;
        }

        keys.Add(AssistModeEnabledKey);
        entries.Add(new JsonObject
        {
            ["m_JSON"] = newInner.ToJsonString(new JsonSerializerOptions { WriteIndented = false }),
        });
    }

    private static void RemoveAssistModeEntry(JsonArray keys, JsonArray entries)
    {
        var index = FindKeyIndex(keys, AssistModeEnabledKey);
        if (index < 0)
        {
            return;
        }

        keys.RemoveAt(index);
        if (index < entries.Count)
        {
            entries.RemoveAt(index);
        }
    }

    private static int FindKeyIndex(JsonArray keys, string targetKey)
    {
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

    private static JsonObject? ParseInnerEntry(JsonNode? entryNode)
    {
        var entryObj = entryNode as JsonObject;
        var jsonText = entryObj?["m_JSON"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            return null;
        }

        return JsonNode.Parse(jsonText)?.AsObject();
    }

    private static void WriteInnerEntry(JsonNode? entryNode, JsonObject inner)
    {
        if (entryNode is not JsonObject obj)
        {
            return;
        }

        obj["m_JSON"] = inner.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static bool TryGetInnerMap(JsonObject inner, out Dictionary<string, JsonNode?> map)
    {
        map = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        var keyArray = inner["m_Key"] as JsonArray;
        var valueArray = inner["m_Value"] as JsonArray;
        if (keyArray is null || valueArray is null)
        {
            return false;
        }

        var count = Math.Min(keyArray.Count, valueArray.Count);
        for (var i = 0; i < count; i++)
        {
            var key = keyArray[i]?.GetValue<string>();
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            map[key] = valueArray[i]?.DeepClone();
        }

        return true;
    }

    private static void SetInnerMap(JsonObject inner, Dictionary<string, JsonNode?> map)
    {
        var keyArray = new JsonArray();
        var valueArray = new JsonArray();

        foreach (var pair in map)
        {
            keyArray.Add(pair.Key);
            valueArray.Add(pair.Value?.DeepClone());
        }

        inner["m_Key"] = keyArray;
        inner["m_Value"] = valueArray;
    }
}

