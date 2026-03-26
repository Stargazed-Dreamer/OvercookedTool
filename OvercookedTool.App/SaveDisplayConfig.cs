using System.Text.Json;
using System.Text.RegularExpressions;
using OvercookedTool.Core.Models;

namespace OvercookedTool.App;

internal sealed class SaveDisplayConfig
{
    private static readonly Lazy<SaveDisplayConfig> LazyInstance = new(Load);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static SaveDisplayConfig Instance => LazyInstance.Value;

    private readonly Dictionary<string, string> _groupDisplayNames;
    private readonly Dictionary<string, string> _fieldTranslations;
    private readonly Dictionary<string, string> _uiTranslations;
    private readonly Dictionary<string, Dictionary<int, string>> _levelMappings;
    private readonly Dictionary<string, int> _groupOrder;
    private readonly Dictionary<string, string> _ruleSegmentTranslations;

    private SaveDisplayConfig(
        Dictionary<string, string> groupDisplayNames,
        Dictionary<string, string> fieldTranslations,
        Dictionary<string, string> uiTranslations,
        Dictionary<string, Dictionary<int, string>> levelMappings,
        Dictionary<string, int> groupOrder,
        Dictionary<string, string> ruleSegmentTranslations)
    {
        _groupDisplayNames = groupDisplayNames;
        _fieldTranslations = fieldTranslations;
        _uiTranslations = uiTranslations;
        _levelMappings = levelMappings;
        _groupOrder = groupOrder;
        _ruleSegmentTranslations = ruleSegmentTranslations;
    }

    public string GetGroupDisplayName(string groupKey, bool translated = true)
    {
        if (!translated)
        {
            return groupKey;
        }

        return _groupDisplayNames.TryGetValue(groupKey, out var value) ? value : groupKey;
    }

    public string GetFieldDisplayName(string fieldKey, bool translated)
    {
        if (!translated)
        {
            return fieldKey;
        }

        if (_fieldTranslations.TryGetValue(fieldKey, out var value))
        {
            return value;
        }

        return TryTranslateByRule(fieldKey, out var auto) ? auto : fieldKey;
    }

    public string GetUiTranslation(string key, string fallback)
    {
        return _uiTranslations.TryGetValue(key, out var value) ? value : fallback;
    }

    public string GetLevelDisplayName(string groupKey, int levelId, string fallback)
    {
        if (_levelMappings.TryGetValue(groupKey, out var map) && map.TryGetValue(levelId, out var name))
        {
            return name;
        }

        return fallback;
    }

    public int GetGroupOrder(string groupKey)
    {
        return _groupOrder.TryGetValue(groupKey, out var order) ? order : int.MaxValue;
    }

    public string GetGroupKey(SaveFileEntry entry)
    {
        if (entry.IsMeta)
        {
            return "Meta";
        }

        if (!string.IsNullOrWhiteSpace(entry.Prefix))
        {
            if (Regex.Match(entry.Prefix, @"^DLC(?<id>\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) is { Success: true } byPrefix)
            {
                return $"DLC{byPrefix.Groups["id"].Value}";
            }

            return entry.Prefix;
        }

        if (entry.DlcId.HasValue)
        {
            return $"DLC{entry.DlcId.Value}";
        }

        if (Regex.Match(entry.FileName, @"DLC(?<id>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) is { Success: true } byName)
        {
            return $"DLC{byName.Groups["id"].Value}";
        }

        return "CoopSlot";
    }

    private static SaveDisplayConfig Load()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "save_display_config.json");
            if (!File.Exists(path))
            {
                return BuildDefault();
            }

            var root = JsonSerializer.Deserialize<ConfigRoot>(File.ReadAllText(path), JsonOptions)
                       ?? new ConfigRoot();

            var groupNames = BuildStringMap(root.GroupDisplayNames, StringComparer.OrdinalIgnoreCase);
            var fieldNames = BuildStringMap(root.FieldTranslations, StringComparer.Ordinal);
            var uiNames = BuildStringMap(root.UiTranslations, StringComparer.Ordinal);
            var ruleSegments = BuildStringMap(root.RuleSegmentTranslations, StringComparer.Ordinal);

            var levelMappings = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var groupPair in root.LevelMappings ?? new Dictionary<string, Dictionary<string, string>>())
            {
                var map = new Dictionary<int, string>();
                foreach (var levelPair in groupPair.Value)
                {
                    if (int.TryParse(levelPair.Key, out var levelId) && !string.IsNullOrWhiteSpace(levelPair.Value))
                    {
                        map[levelId] = levelPair.Value;
                    }
                }

                levelMappings[groupPair.Key] = map;
            }

            var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (root.GroupOrder is not null)
            {
                for (var i = 0; i < root.GroupOrder.Count; i++)
                {
                    var key = root.GroupOrder[i];
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        order[key] = i;
                    }
                }
            }

            return new SaveDisplayConfig(groupNames, fieldNames, uiNames, levelMappings, order, ruleSegments);
        }
        catch
        {
            return BuildDefault();
        }
    }

    private static Dictionary<string, string> BuildStringMap(Dictionary<string, string>? source, StringComparer comparer)
    {
        var map = new Dictionary<string, string>(comparer);
        foreach (var pair in source ?? new Dictionary<string, string>())
        {
            if (!string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            {
                map[pair.Key] = pair.Value;
            }
        }

        return map;
    }

    private static SaveDisplayConfig BuildDefault()
    {
        var groupNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Meta"] = "元数据",
            ["CoopSlot"] = "2代主线",
            ["OC1"] = "1代主线",
            ["BAG"] = "冒险故事",
        };

        var fieldNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["LevelID"] = "关卡ID",
            ["Completed"] = "是否完成",
            ["Purchased"] = "是否可玩",
            ["Revealed"] = "是否显示",
            ["HighScore"] = "分数",
            ["ScoreStars"] = "星级",
            ["ObjectivesCompleted"] = "隐藏目标达成",
            ["FailedAttempts"] = "失败次数",
            ["SurvivalModeTime"] = "生存时长",
            ["LastLevelEntered"] = "上次进入关卡",
            ["Switches_Revealed"] = "已揭示开关",
            ["VERSION"] = "版本号",
            ["Level_Count"] = "关卡数量",
            ["AvatarUnlocks"] = "厨师解锁",
            ["LastSaveUsed"] = "上次使用存档",
            ["LastThemePlayed"] = "上次主题",
            ["NGPEnabled"] = "新游戏+开启",
            ["AssistModeEnabled"] = "辅助模式开启",
            ["AssistModeCompleted"] = "辅助模式通关",
            ["NewGamePlusEnabled"] = "新游戏+开启",
            ["NewGamePlusDialogShown"] = "新游戏+提示已显示",
        };

        var uiNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["LevelHeader"] = "关卡",
            ["MetaKeyHeader"] = "字段",
            ["MetaValueHeader"] = "值",
        };

        var ruleSegments = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Timer"] = "计时器",
            ["Score"] = "分数",
            ["OrderExpiration"] = "订单过期",
            ["ThrownItemCount"] = "投掷次数",
            ["RecipesDelivered"] = "送餐数量",
            ["RecipesDeliveredInOrder"] = "按序送餐数",
            ["RecipeIds"] = "菜谱ID",
            ["ChefIds"] = "厨师ID",
            ["EmoteIds"] = "表情ID",
            ["WorldsCompleteIds"] = "已完成世界ID",
            ["SwitchesHit"] = "开关触发次数",
            ["ExtinguishBurningKitchen"] = "灭火次数",
            ["ChopCount"] = "切菜次数",
            ["BinnedItemCount"] = "丢弃次数",
            ["BurnedItemCount"] = "烧糊次数",
            ["CaughtItemCount"] = "接住物品次数",
            ["CaughtItemInPotCount"] = "接锅次数",
            ["PortalPassedThroughCount"] = "传送门通过次数",
            ["PlatesWashed"] = "洗盘次数",
            ["DLC05_RecipeIds"] = "DLC5菜谱ID",
            ["DLC05_World0Ids"] = "DLC5世界0 ID",
            ["DLC05_BurntWoodCount"] = "DLC5烧木次数",
            ["DLC05_ItemsTakenFromBackpack"] = "DLC5背包取物次数",
            ["DLC08_CondimentUseCount"] = "DLC8调味料使用次数",
            ["DLC08_CannonFireCount"] = "DLC8大炮发射次数",
        };

        return new SaveDisplayConfig(
            groupNames,
            fieldNames,
            uiNames,
            new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            ruleSegments);
    }

    private bool TryTranslateByRule(string key, out string translated)
    {
        translated = string.Empty;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (key.StartsWith("P_OptionType.", StringComparison.Ordinal))
        {
            var option = key["P_OptionType.".Length..];
            translated = "设置." + TranslateSegment(option);
            return true;
        }

        if (key.StartsWith("GameModeSetting ", StringComparison.Ordinal))
        {
            var mode = key["GameModeSetting ".Length..];
            translated = "模式设置." + TranslateSegment(mode);
            return true;
        }

        if (key.StartsWith("LastSaveUsed_DLC", StringComparison.Ordinal))
        {
            translated = "上次使用存档(DLC" + key["LastSaveUsed_DLC".Length..] + ")";
            return true;
        }

        if (key.StartsWith("DLC_", StringComparison.Ordinal) && int.TryParse(key["DLC_".Length..], out var dlc))
        {
            translated = $"DLC可用标记 {dlc}";
            return true;
        }

        if (key.StartsWith("Switch_", StringComparison.Ordinal) && int.TryParse(key["Switch_".Length..], out var n))
        {
            translated = $"开关{n}";
            return true;
        }

        if (key.StartsWith("MetaDialogShown_", StringComparison.Ordinal) && int.TryParse(key["MetaDialogShown_".Length..], out var dialogId))
        {
            translated = $"元数据弹窗标记 {dialogId}";
            return true;
        }

        if (key.StartsWith("Teleportal_", StringComparison.Ordinal))
        {
            translated = "传送门状态." + key["Teleportal_".Length..];
            return true;
        }

        if (key.StartsWith("UserKeyBindings_", StringComparison.Ordinal))
        {
            translated = "用户键位." + key["UserKeyBindings_".Length..];
            return true;
        }

        if (TryTranslateStatsKey(key, out translated))
        {
            return true;
        }

        if (key.StartsWith("S_", StringComparison.Ordinal))
        {
            translated = "统计." + key["S_".Length..];
            return true;
        }

        return false;
    }

    private bool TryTranslateStatsKey(string key, out string translated)
    {
        translated = string.Empty;

        var matched = Regex.Match(key, @"^S_(?<name>.+?)_HS_(?<slot>\d+)$", RegexOptions.CultureInvariant);
        if (matched.Success)
        {
            translated = $"统计.{TranslateSegment(matched.Groups["name"].Value)}.集合槽位({matched.Groups["slot"].Value})";
            return true;
        }

        matched = Regex.Match(key, @"^S_(?<name>.+?)_KEY_(?<slot>\d+)_(?<index>\d+)$", RegexOptions.CultureInvariant);
        if (matched.Success)
        {
            translated = $"统计.{TranslateSegment(matched.Groups["name"].Value)}.键({matched.Groups["slot"].Value}:{matched.Groups["index"].Value})";
            return true;
        }

        matched = Regex.Match(key, @"^S_(?<name>.+?)_V_(?<slot>\d+)(?:_(?<index>\d+))?$", RegexOptions.CultureInvariant);
        if (matched.Success)
        {
            var index = matched.Groups["index"].Success ? ":" + matched.Groups["index"].Value : string.Empty;
            translated = $"统计.{TranslateSegment(matched.Groups["name"].Value)}.值({matched.Groups["slot"].Value}{index})";
            return true;
        }

        return false;
    }

    private string TranslateSegment(string segment)
    {
        if (_ruleSegmentTranslations.TryGetValue(segment, out var mapped))
        {
            return mapped;
        }

        return Regex.Replace(
            segment,
            @"[A-Za-z0-9]+",
            m => _ruleSegmentTranslations.TryGetValue(m.Value, out var token) ? token : m.Value,
            RegexOptions.CultureInvariant);
    }

    private sealed class ConfigRoot
    {
        public List<string>? GroupOrder { get; init; }
        public Dictionary<string, string>? GroupDisplayNames { get; init; }
        public Dictionary<string, string>? FieldTranslations { get; init; }
        public Dictionary<string, string>? UiTranslations { get; init; }
        public Dictionary<string, string>? RuleSegmentTranslations { get; init; }
        public Dictionary<string, Dictionary<string, string>>? LevelMappings { get; init; }
    }
}

