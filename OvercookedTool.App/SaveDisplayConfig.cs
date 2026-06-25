using System.Text.Json;
using System.Text.RegularExpressions;
using OvercookedTool.Core.Models;

namespace OvercookedTool.App;

internal sealed class SaveDisplayConfig
{
    /// <summary>
    /// 存档显示配置的单例类。
    /// 负责管理游戏存档中各种键值（如组名、字段名、UI文本等）的显示翻译与映射。
    /// 支持从JSON配置文件加载自定义翻译，并提供基于规则的自动翻译功能。
    /// </summary>
    private static readonly Lazy<SaveDisplayConfig> LazyInstance = new(Load);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 获取当前实例（线程安全的单例访问）。
    /// </summary>
    public static SaveDisplayConfig Instance => LazyInstance.Value;

    // 组键到其显示名称的映射字典
    private readonly Dictionary<string, string> _groupDisplayNames;
    // 字段键到其显示名称的映射字典
    private readonly Dictionary<string, string> _fieldTranslations;
    // 通用UI文本键到其翻译值的映射字典
    private readonly Dictionary<string, string> _uiTranslations;
    // 组键到 {等级ID -> 等级显示名称} 映射的字典
    private readonly Dictionary<string, Dictionary<int, string>> _levelMappings;
    // 组键到显示顺序值的映射字典
    private readonly Dictionary<string, int> _groupOrder;
    // 规则片段到其翻译文本的映射字典
    private readonly Dictionary<string, string> _ruleSegmentTranslations;

    /// <summary>
    /// 私有构造函数，用于通过工厂方法初始化实例。
    /// </summary>
    /// <param name="groupDisplayNames">组显示名称映射</param>
    /// <param name="fieldTranslations">字段名翻译映射</param>
    /// <param name="uiTranslations">UI文本翻译映射</param>
    /// <param name="levelMappings">等级名称映射</param>
    /// <param name="groupOrder">组显示顺序</param>
    /// <param name="ruleSegmentTranslations">规则片段翻译映射</param>
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

    /// <summary>
    /// 获取组的显示名称。
    /// </summary>
    /// <param name="groupKey">组键</param>
    /// <param name="translated">是否需要翻译（默认为true）</param>
    /// <returns>翻译后的显示名称，若未找到翻译则返回原始键</returns>
    public string GetGroupDisplayName(string groupKey, bool translated = true)
    {
        if (!translated)
        {
            return groupKey;
        }

        return _groupDisplayNames.TryGetValue(groupKey, out var value) ? value : groupKey;
    }

    /// <summary>
    /// 获取字段的显示名称。
    /// </summary>
    /// <param name="fieldKey">字段键</param>
    /// <param name="translated">是否需要翻译</param>
    /// <returns>翻译后的显示名称，若未找到翻译则尝试基于规则翻译，否则返回原始键</returns>
    public string GetFieldDisplayName(string fieldKey, bool translated)
    {
        if (!translated)
        {
            return fieldKey;
        }

        // 先尝试精确匹配翻译字典
        if (_fieldTranslations.TryGetValue(fieldKey, out var value))
        {
            return value;
        }

        // 精确匹配失败，尝试基于规则进行自动翻译
        return TryTranslateByRule(fieldKey, out var auto) ? auto : fieldKey;
    }

    /// <summary>
    /// 获取UI文本的翻译。
    /// </summary>
    /// <param name="key">文本键</param>
    /// <param name="fallback">未找到翻译时的回退值</param>
    /// <returns>翻译值或回退值</returns>
    public string GetUiTranslation(string key, string fallback)
    {
        return _uiTranslations.TryGetValue(key, out var value) ? value : fallback;
    }

    /// <summary>
    /// 获取指定组下某个等级的显示名称。
    /// </summary>
    /// <param name="groupKey">组键</param>
    /// <param name="levelId">等级ID</param>
    /// <param name="fallback">未找到翻译时的回退值</param>
    /// <returns>等级显示名称或回退值</returns>
    public string GetLevelDisplayName(string groupKey, int levelId, string fallback)
    {
        // 先查找组的等级映射，再在该映射中查找特定等级ID
        if (_levelMappings.TryGetValue(groupKey, out var map) && map.TryGetValue(levelId, out var name))
        {
            return name;
        }

        return fallback;
    }

    /// <summary>
    /// 获取组的显示顺序值。
    /// </summary>
    /// <param name="groupKey">组键</param>
    /// <returns>顺序值，若未定义则返回int.MaxValue（表示最后显示）</returns>
    public int GetGroupOrder(string groupKey)
    {
        return _groupOrder.TryGetValue(groupKey, out var order) ? order : int.MaxValue;
    }

    /// <summary>
    /// 根据存档文件条目信息，确定其所属的组键。
    /// </summary>
    /// <param name="entry">存档文件条目</param>
    /// <returns>推断出的组键（如 "Meta", "DLC1", "CoopSlot" 等）</returns>
    public string GetGroupKey(SaveFileEntry entry)
    {
        // 条目是元数据类型，直接归入 "Meta" 组
        if (entry.IsMeta)
        {
            return "Meta";
        }

        // 如果条目有非空前缀
        if (!string.IsNullOrWhiteSpace(entry.Prefix))
        {
            // 尝试匹配前缀中的DLC编号模式
            if (Regex.Match(entry.Prefix, @"^DLC(?<id>\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) is { Success: true } byPrefix)
            {
                return $"DLC{byPrefix.Groups["id"].Value}";
            }

            // 匹配失败则使用原始前缀作为组键
            return entry.Prefix;
        }

        // 如果条目有显式的DLC ID属性
        if (entry.DlcId.HasValue)
        {
            return $"DLC{entry.DlcId.Value}";
        }

        // 尝试从文件名中匹配DLC编号模式
        if (Regex.Match(entry.FileName, @"DLC(?<id>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) is { Success: true } byName)
        {
            return $"DLC{byName.Groups["id"].Value}";
        }

        // 以上条件都不满足，归为合作模式插槽组
        return "CoopSlot";
    }

    /// <summary>
    /// 从JSON文件加载配置并创建实例的工厂方法（供Lazy调用）。
    /// </summary>
    /// <returns>初始化的SaveDisplayConfig实例</returns>
    private static SaveDisplayConfig Load()
    {
        try
        {
            // 构建配置文件路径
            var path = Path.Combine(AppContext.BaseDirectory, "save_display_config.json");
            if (!File.Exists(path))
            {
                // 文件不存在，使用默认配置
                return BuildDefault();
            }

            // 读取并反序列化JSON配置文件
            var root = JsonSerializer.Deserialize<ConfigRoot>(File.ReadAllText(path), JsonOptions)
                       ?? new ConfigRoot();

            // 使用不区分大小写的比较器构建组名映射
            var groupNames = BuildStringMap(root.GroupDisplayNames, StringComparer.OrdinalIgnoreCase);
            // 使用区分大小写的比较器构建字段名映射
            var fieldNames = BuildStringMap(root.FieldTranslations, StringComparer.Ordinal);
            // 使用区分大小写的比较器构建UI名称映射
            var uiNames = BuildStringMap(root.UiTranslations, StringComparer.Ordinal);
            // 使用区分大小写的比较器构建规则片段映射
            var ruleSegments = BuildStringMap(root.RuleSegmentTranslations, StringComparer.Ordinal);

            // 处理等级映射：外层字典键是组名，内层需要将字符串键转换为整数ID
            var levelMappings = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var groupPair in root.LevelMappings ?? new Dictionary<string, Dictionary<string, string>>())
            {
                var map = new Dictionary<int, string>();
                foreach (var levelPair in groupPair.Value)
                {
                    // 尝试将字符串键解析为整数等级ID，并且值非空
                    if (int.TryParse(levelPair.Key, out var levelId) && !string.IsNullOrWhiteSpace(levelPair.Value))
                    {
                        map[levelId] = levelPair.Value;
                    }
                }

                levelMappings[groupPair.Key] = map;
            }

            // 处理组顺序：将列表的索引作为顺序值
            var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (root.GroupOrder is not null)
            {
                for (var i = 0; i < root.GroupOrder.Count; i++)
                {
                    var key = root.GroupOrder[i];
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        order[key] = i; // 列表中的位置即为顺序
                    }
                }
            }

            return new SaveDisplayConfig(groupNames, fieldNames, uiNames, levelMappings, order, ruleSegments);
        }
        catch
        {
            // 加载过程中发生任何异常（如文件损坏、格式错误），都回退到默认配置
            return BuildDefault();
        }
    }

    /// <summary>
    /// 从源字典构建一个新字典，过滤掉键或值为空白的条目。
    /// </summary>
    /// <param name="source">源字典（可为null）</param>
    /// <param name="comparer">键的比较器</param>
    /// <returns>过滤后的新字典</returns>
    private static Dictionary<string, string> BuildStringMap(Dictionary<string, string>? source, StringComparer comparer)
    {
        var map = new Dictionary<string, string>(comparer);
        foreach (var pair in source ?? new Dictionary<string, string>())
        {
            // 只保留键和值都非空白的条目
            if (!string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            {
                map[pair.Key] = pair.Value;
            }
        }

        return map;
    }

    /// <summary>
    /// 构建默认的、硬编码的配置实例。
    /// </summary>
    /// <returns>包含默认翻译的SaveDisplayConfig实例</returns>
    private static SaveDisplayConfig BuildDefault()
    {
        // 默认的组显示名称（键 -> 中文名）
        var groupNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Meta"] = "元数据",
            ["CoopSlot"] = "2代主线",
            ["OC1"] = "1代主线",
            ["BAG"] = "冒险故事",
        };

        // 默认的字段显示名称（键 -> 中文名）
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

        // 默认的UI文本翻译
        var uiNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["LevelHeader"] = "关卡",
            ["MetaKeyHeader"] = "字段",
            ["MetaValueHeader"] = "值",
        };

        // 默认的规则片段翻译（用于自动翻译）
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

        // 使用默认值创建实例，等级映射和组顺序字典为空
        return new SaveDisplayConfig(
            groupNames,
            fieldNames,
            uiNames,
            new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            ruleSegments);
    }

    /// <summary>
    /// 尝试根据预定义的规则（前缀）对键进行自动翻译。
    /// </summary>
    /// <param name="key">需要翻译的键</param>
    /// <param name="translated">输出：翻译结果</param>
    /// <returns>是否成功翻译</returns>
    private bool TryTranslateByRule(string key, out string translated)
    {
        translated = string.Empty;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        // 规则1：键以 "P_OptionType." 开头，翻译为 "设置." + 片段翻译
        if (key.StartsWith("P_OptionType.", StringComparison.Ordinal))
        {
            var option = key["P_OptionType.".Length..];
            translated = "设置." + TranslateSegment(option);
            return true;
        }

        // 规则2：键以 "GameModeSetting " 开头，翻译为 "模式设置." + 片段翻译
        if (key.StartsWith("GameModeSetting ", StringComparison.Ordinal))
        {
            var mode = key["GameModeSetting ".Length..];
            translated = "模式设置." + TranslateSegment(mode);
            return true;
        }

        // 规则3：键以 "LastSaveUsed_DLC" 开头，拼接为 "上次使用存档(DLC...)"
        if (key.StartsWith("LastSaveUsed_DLC", StringComparison.Ordinal))
        {
            translated = "上次使用存档(DLC" + key["LastSaveUsed_DLC".Length..] + ")";
            return true;
        }

        // 规则4：键以 "DLC_" 开头，后面是数字，翻译为 "DLC可用标记 {数字}"
        if (key.StartsWith("DLC_", StringComparison.Ordinal) && int.TryParse(key["DLC_".Length..], out var dlc))
        {
            translated = $"DLC可用标记 {dlc}";
            return true;
        }

        // 规则5：键以 "Switch_" 开头，后面是数字，翻译为 "开关{数字}"
        if (key.StartsWith("Switch_", StringComparison.Ordinal) && int.TryParse(key["Switch_".Length..], out var n))
        {
            translated = $"开关{n}";
            return true;
        }

        // 规则6：键以 "MetaDialogShown_" 开头，后面是数字，翻译为 "元数据弹窗标记 {数字}"
        if (key.StartsWith("MetaDialogShown_", StringComparison.Ordinal) && int.TryParse(key["MetaDialogShown_".Length..], out var dialogId))
        {
            translated = $"元数据弹窗标记 {dialogId}";
            return true;
        }

        // 规则7：键以 "Teleportal_" 开头，翻译为 "传送门状态." + 剩余部分
        if (key.StartsWith("Teleportal_", StringComparison.Ordinal))
        {
            translated = "传送门状态." + key["Teleportal_".Length..];
            return true;
        }

        // 规则8：键以 "UserKeyBindings_" 开头，翻译为 "用户键位." + 剩余部分
        if (key.StartsWith("UserKeyBindings_", StringComparison.Ordinal))
        {
            translated = "用户键位." + key["UserKeyBindings_".Length..];
            return true;
        }

        // 规则9：尝试匹配更复杂的统计键模式
        if (TryTranslateStatsKey(key, out translated))
        {
            return true;
        }

        // 规则10：键以 "S_" 开头，翻译为 "统计." + 剩余部分
        if (key.StartsWith("S_", StringComparison.Ordinal))
        {
            translated = "统计." + key["S_".Length..];
            return true;
        }

        // 没有匹配任何规则
        return false;
    }

    /// <summary>
    /// 尝试翻译复杂的统计键（S_...）。
    /// 使用正则表达式匹配特定模式。
    /// </summary>
    /// <param name="key">需要翻译的键</param>
    /// <param name="translated">输出：翻译结果</param>
    /// <returns>是否成功匹配并翻译</returns>
    private bool TryTranslateStatsKey(string key, out string translated)
    {
        translated = string.Empty;

        // 模式1：S_{name}_HS_{slot} → 统计.{name翻译}.集合槽位({slot})
        var matched = Regex.Match(key, @"^S_(?<name>.+?)_HS_(?<slot>\d+)$", RegexOptions.CultureInvariant);
        if (matched.Success)
        {
            translated = $"统计.{TranslateSegment(matched.Groups["name"].Value)}.集合槽位({matched.Groups["slot"].Value})";
            return true;
        }

        // 模式2：S_{name}_KEY_{slot}_{index} → 统计.{name翻译}.键({slot}:{index})
        matched = Regex.Match(key, @"^S_(?<name>.+?)_KEY_(?<slot>\d+)_(?<index>\d+)$", RegexOptions.CultureInvariant);
        if (matched.Success)
        {
            translated = $"统计.{TranslateSegment(matched.Groups["name"].Value)}.键({matched.Groups["slot"].Value}:{matched.Groups["index"].Value})";
            return true;
        }

        // 模式3：S_{name}_V_{slot} 或 S_{name}_V_{slot}_{index} → 统计.{name翻译}.值({slot}[:{index}])
        matched = Regex.Match(key, @"^S_(?<name>.+?)_V_(?<slot>\d+)(?:_(?<index>\d+))?$", RegexOptions.CultureInvariant);
        if (matched.Success)
        {
            // 如果存在可选的index组，则拼接上
            var index = matched.Groups["index"].Success ? ":" + matched.Groups["index"].Value : string.Empty;
            translated = $"统计.{TranslateSegment(matched.Groups["name"].Value)}.值({matched.Groups["slot"].Value}{index})";
            return true;
        }

        return false;
    }

    /// <summary>
    /// 翻译一个字符串片段。
    /// 优先查找字典精确匹配，失败则尝试按单词（连续字母数字）翻译。
    /// </summary>
    /// <param name="segment">需要翻译的片段</param>
    /// <returns>翻译后的片段</returns>
    private string TranslateSegment(string segment)
    {
        // 首先尝试在规则片段字典中精确查找
        if (_ruleSegmentTranslations.TryGetValue(segment, out var mapped))
        {
            return mapped;
        }

        // 精确匹配失败，使用正则表达式替换片段中的每个连续字母数字单词
        return Regex.Replace(
            segment,
            @"[A-Za-z0-9]+", // 匹配连续的英文字母或数字
            // 对于每个匹配到的单词，尝试在字典中查找翻译，找不到则保留原词
            m => _ruleSegmentTranslations.TryGetValue(m.Value, out var token) ? token : m.Value,
            RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// JSON配置文件的根对象模型。
    /// </summary>
    private sealed class ConfigRoot
    {
        /// <summary>
        /// 组显示顺序列表（列表顺序即显示顺序）。
        /// </summary>
        public List<string>? GroupOrder { get; init; }
        /// <summary>
        /// 组键到显示名称的映射。
        /// </summary>
        public Dictionary<string, string>? GroupDisplayNames { get; init; }
        /// <summary>
        /// 字段键到显示名称的映射。
        /// </summary>
        public Dictionary<string, string>? FieldTranslations { get; init; }
        /// <summary>
        /// 通用UI文本键到翻译值的映射。
        /// </summary>
        public Dictionary<string, string>? UiTranslations { get; init; }
        /// <summary>
        /// 规则片段键到翻译文本的映射（用于自动翻译）。
        /// </summary>
        public Dictionary<string, string>? RuleSegmentTranslations { get; init; }
        /// <summary>
        /// 等级映射。外层键为组名，内层字典键为等级ID（字符串形式），值为等级显示名称。
        /// </summary>
        public Dictionary<string, Dictionary<string, string>>? LevelMappings { get; init; }
    }
}

