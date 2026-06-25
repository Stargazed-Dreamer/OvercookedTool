using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OvercookedTool.Core.Models;

namespace OvercookedTool.App;

/// <summary>
/// 用于编辑存档数据的表单，将 JSON 格式的存档解析并显示在表格中以供编辑和保存。
/// </summary>
internal sealed class SaveTableEditorForm : Form
{
    private readonly DataGridView _table;
    private readonly CheckBox _translateCheck;
    private readonly JsonObject _root;
    private readonly SaveDisplayConfig _config;
    private readonly string _groupKey;
    private readonly List<LevelRow> _rows;
    private readonly List<ColumnDef> _fieldColumns = new();
    private readonly Dictionary<string, int> _fieldColumnIndex = new(StringComparer.Ordinal);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string JsonText { get; private set; }

    /// <summary>
    /// 构造函数，初始化编辑表单。
    /// </summary>
    /// <param name="save">存档文件条目信息。</param>
    /// <param name="version">存档版本。</param>
    /// <param name="jsonText">原始的 JSON 文本。</param>
    /// <param name="translateDefault">是否默认启用翻译显示。</param>
    public SaveTableEditorForm(SaveFileEntry save, SaveVersion version, string jsonText, bool translateDefault = true)
    {
        _config = SaveDisplayConfig.Instance;
        _groupKey = _config.GetGroupKey(save);

        Text = $"编辑存档 - {save.FileName}";
        Width = 980;
        Height = 720;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 620);

        // 解析 JSON，获取根节点和关卡数据行列表
        (_root, _rows) = ParseLevels(jsonText);
        JsonText = jsonText;

        // 创建主布局容器
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        // 创建顶部信息和控制栏布局
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // 显示存档文件、版本、分组和关卡数量的信息标签
        var infoLabel = new Label
        {
            AutoSize = true,
            Text = $"文件: {save.FileName}  |  版本: {version}  |  分组: {_config.GetGroupDisplayName(_groupKey)} ({_groupKey})  |  关卡记录: {_rows.Count}",
            Margin = new Padding(0, 6, 0, 6),
        };
        header.Controls.Add(infoLabel, 0, 0);

        // 翻译显示复选框
        _translateCheck = new CheckBox
        {
            Text = "启用翻译显示",
            AutoSize = true,
            Checked = translateDefault,
            Margin = new Padding(8, 4, 0, 0),
        };
        // 复选框状态改变时刷新表格的翻译显示
        _translateCheck.CheckedChanged += (_, _) => RefreshTranslation();
        header.Controls.Add(_translateCheck, 1, 0);
        root.Controls.Add(header, 0, 0);

        // 主 DataGridView 表格控件，用于显示和编辑关卡数据
        _table = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            EditMode = DataGridViewEditMode.EditOnEnter,
        };
        root.Controls.Add(_table, 0, 1);

        // 底部按钮行布局
        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0),
        };
        var saveButton = new Button { Text = "保存", AutoSize = true };
        // 点击保存按钮时保存数据并关闭窗体
        saveButton.Click += (_, _) => SaveAndClose();
        var cancelButton = new Button { Text = "取消", AutoSize = true };
        // 点击取消按钮时取消操作并关闭窗体
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        buttonRow.Controls.Add(saveButton);
        buttonRow.Controls.Add(cancelButton);
        root.Controls.Add(buttonRow, 0, 2);

        // 构建表格列
        BuildColumns();
        // 填充表格数据
        PopulateTable();
    }

    /// <summary>
    /// 解析 JSON 文本，提取有效的关卡数据行。
    /// </summary>
    /// <param name="jsonText">要解析的 JSON 文本。</param>
    /// <returns>返回根 JSON 对象和解析后的关卡数据行列表的元组。</returns>
    private static (JsonObject Root, List<LevelRow> Rows) ParseLevels(string jsonText)
    {
        var root = JsonNode.Parse(jsonText)?.AsObject()
                   ?? throw new InvalidOperationException("存档内容不是有效 JSON。");
        var keys = root["m_Keys"] as JsonArray
                   ?? throw new InvalidOperationException("存档缺少 m_Keys。");
        var entries = root["m_Entries"] as JsonArray
                      ?? throw new InvalidOperationException("存档缺少 m_Entries。");

        // 取键和条目数量的最小值进行遍历
        var count = Math.Min(keys.Count, entries.Count);
        var rows = new List<LevelRow>();
        for (var i = 0; i < count; i++)
        {
            var key = keys[i]?.GetValue<string>();
            // 只处理以 "Level_" 开头且后跟数字的键（关卡数据键）
            if (!IsLevelDataKey(key))
            {
                continue;
            }

            if (entries[i] is not JsonObject outer)
            {
                continue;
            }

            // 从外部对象中获取内嵌的 JSON 字符串
            var innerText = outer["m_JSON"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(innerText))
            {
                continue;
            }

            // 解析内嵌的 JSON 字符串为内部对象
            var inner = JsonNode.Parse(innerText)?.AsObject();
            if (inner is null)
            {
                continue;
            }

            // 将内部 JSON 对象转换为键值对字典，便于后续编辑
            var map = ToInnerMap(inner);
            // 尝试从字典或键名中解析关卡 ID
            var levelId = TryGetInt(map, "LevelID") ?? TryParseLevelId(key!) ?? 0;

            rows.Add(new LevelRow
            {
                LevelKey = key!,
                Outer = outer,
                Inner = inner,
                Map = map,
                LevelId = levelId,
            });
        }

        if (rows.Count == 0)
        {
            throw new InvalidOperationException("未找到可编辑的关卡表（Level_*）。");
        }

        // 按关卡 ID 排序，ID 相同时再按键名排序
        return (root, rows.OrderBy(x => x.LevelId).ThenBy(x => x.LevelKey).ToList());
    }

    /// <summary>
    /// 构建 DataGridView 的列结构，基于解析出的字段。
    /// </summary>
    private void BuildColumns()
    {
        _table.Columns.Clear();
        _fieldColumns.Clear();
        _fieldColumnIndex.Clear();

        // 添加固定的第一列：关卡显示列（只读）
        _table.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "level_display",
            HeaderText = GetLevelHeaderText(),
            ReadOnly = true,
            Width = 220,
        });

        // 定义字段列的优先显示顺序
        var preferredOrder = new[]
        {
            "LevelID", "ScoreStars", "HighScore", "Completed", "Purchased", "Revealed",
            "ObjectivesCompleted", "AssistModeCompleted", "AssistModeEnabled", "NGPEnabled",
            "FailedAttempts", "SurvivalModeTime",
        };
        // 创建排序映射，用于后续字段排序
        var orderMap = preferredOrder
            .Select((key, idx) => (key, idx))
            .ToDictionary(x => x.key, x => x.idx, StringComparer.Ordinal);

        // 从所有行中提取所有唯一的字段键，并按优先顺序排序
        var keys = _rows
            .SelectMany(x => x.Map.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => orderMap.TryGetValue(x, out var idx) ? idx : int.MaxValue)
            .ThenBy(x => x, StringComparer.Ordinal)
            .ToList();

        // 遍历所有字段键，为每个键创建对应的列
        foreach (var key in keys)
        {
            var isBool = IsBoolField(key);
            // 根据字段类型创建复选框列或文本框列
            DataGridViewColumn column = isBool
                ? new DataGridViewCheckBoxColumn()
                : new DataGridViewTextBoxColumn();
            column.Name = key;
            column.HeaderText = _config.GetFieldDisplayName(key, translated: _translateCheck.Checked);
            // LevelID 字段设为只读
            column.ReadOnly = string.Equals(key, "LevelID", StringComparison.Ordinal);
            // 为不同字段设置合适的列宽
            column.Width = key switch
            {
                "ScoreStars" => 75,
                "HighScore" => 90,
                _ => isBool ? 92 : 110,
            };
            _table.Columns.Add(column);
            // 记录列的定义信息（键、是否布尔、是否只读）
            _fieldColumns.Add(new ColumnDef(key, isBool, column.ReadOnly));
            // 记录字段键到列索引的映射关系
            _fieldColumnIndex[key] = _table.Columns.Count - 1;
        }
    }

    /// <summary>
    /// 将解析出的关卡数据行填充到 DataGridView 表格中。
    /// </summary>
    private void PopulateTable()
    {
        _table.Rows.Clear();
        foreach (var row in _rows)
        {
            // 添加新行并获取行索引
            var rowIndex = _table.Rows.Add();
            var gridRow = _table.Rows[rowIndex];
            // 设置第一列（关卡显示列）的值
            gridRow.Cells[0].Value = GetDisplayLevel(row);
            // 遍历所有字段列，填充对应单元格的值
            foreach (var field in _fieldColumns)
            {
                var columnIndex = _fieldColumnIndex[field.Key];
                // 如果当前行的 Map 中不包含该字段，或值为空，则设置默认值
                if (!row.Map.TryGetValue(field.Key, out var value) || value is null)
                {
                    gridRow.Cells[columnIndex].Value = field.IsBool ? false : string.Empty;
                    continue;
                }

                // 根据字段类型设置单元格值：布尔字段尝试解析为布尔，否则转为字符串
                if (field.IsBool)
                {
                    gridRow.Cells[columnIndex].Value = TryParseBool(value.ToString());
                }
                else
                {
                    gridRow.Cells[columnIndex].Value = value.ToString();
                }
            }
        }
    }

    /// <summary>
    /// 根据翻译复选框的状态，刷新表格的标题和关卡显示内容。
    /// </summary>
    private void RefreshTranslation()
    {
        // 更新第一列的标题
        _table.Columns[0].HeaderText = GetLevelHeaderText();
        // 更新所有字段列的标题
        foreach (var field in _fieldColumns)
        {
            if (_fieldColumnIndex.TryGetValue(field.Key, out var index))
            {
                _table.Columns[index].HeaderText = _config.GetFieldDisplayName(field.Key, _translateCheck.Checked);
            }
        }

        // 更新第一列的单元格显示内容（关卡名称）
        for (var i = 0; i < _rows.Count && i < _table.Rows.Count; i++)
        {
            _table.Rows[i].Cells[0].Value = GetDisplayLevel(_rows[i]);
        }
    }

    /// <summary>
    /// 根据翻译设置获取关卡行的显示文本。
    /// </summary>
    /// <param name="row">要显示的关卡行数据。</param>
    /// <returns>返回关卡的显示文本。</returns>
    private string GetDisplayLevel(LevelRow row)
    {
        // 如果未启用翻译，直接返回原始关卡键名
        if (!_translateCheck.Checked)
        {
            return row.LevelKey;
        }

        // 使用配置获取本地化的关卡显示名称，若失败则回退到默认格式
        var fallback = $"Level_{row.LevelId}";
        return _config.GetLevelDisplayName(_groupKey, row.LevelId, fallback);
    }

    /// <summary>
    /// 根据翻译设置获取关卡列表头的显示文本。
    /// </summary>
    /// <returns>返回列表头文本。</returns>
    private string GetLevelHeaderText()
    {
        // 根据翻译状态返回不同的列表头文本
        return _translateCheck.Checked
            ? _config.GetUiTranslation("LevelHeader", "关卡")
            : "Level";
    }

    /// <summary>
    /// 将表格中的编辑结果保存回 JSON 并关闭窗体。
    /// </summary>
    private void SaveAndClose()
    {
        try
        {
            // 结束当前正在编辑的单元格
            _table.EndEdit();
            // 遍历所有行，将表格中的修改同步回数据模型
            for (var i = 0; i < _rows.Count; i++)
            {
                var ui = _table.Rows[i];
                var model = _rows[i];

                // 遍历所有字段列
                foreach (var field in _fieldColumns)
                {
                    // 跳过在映射中找不到的列
                    if (!_fieldColumnIndex.TryGetValue(field.Key, out var columnIndex))
                    {
                        continue;
                    }

                    // 跳过只读字段
                    if (field.ReadOnly)
                    {
                        continue;
                    }

                    // 处理布尔类型字段：将单元格值转换为布尔，再转为字符串存入 Map
                    if (field.IsBool)
                    {
                        var b = Convert.ToBoolean(ui.Cells[columnIndex].Value ?? false);
                        model.Map[field.Key] = b ? "True" : "False";
                        continue;
                    }

                    // 处理文本类型字段：获取单元格值并清理空格
                    var raw = Convert.ToString(ui.Cells[columnIndex].Value)?.Trim() ?? string.Empty;
                    // 对特定字段（ScoreStars, HighScore, FailedAttempts）进行数据验证
                    if (string.Equals(field.Key, "ScoreStars", StringComparison.Ordinal) && int.TryParse(raw, out var stars))
                    {
                        // 限制 ScoreStars 在 0-4 之间
                        raw = Math.Clamp(stars, 0, 4).ToString();
                    }
                    else if ((string.Equals(field.Key, "HighScore", StringComparison.Ordinal)
                           || string.Equals(field.Key, "FailedAttempts", StringComparison.Ordinal))
                           && !int.TryParse(raw, out _))
                    {
                        // HighScore 和 FailedAttempts 必须为整数
                        throw new InvalidOperationException($"第 {i + 1} 行字段 {field.Key} 需要数字。");
                    }

                    // 将验证后的值存回模型的 Map 中
                    model.Map[field.Key] = raw;
                }

                // 将修改后的 Map 写回内部 JSON 对象
                SetInnerMap(model.Inner, model.Map);
                // 将内部 JSON 对象序列化后更新回外部 JSON 对象的 m_JSON 字段
                model.Outer["m_JSON"] = model.Inner.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
            }

            // 将修改后的整个根 JSON 对象序列化为文本，并设置给公共属性
            JsonText = _root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
            // 设置对话框结果为 OK
            DialogResult = DialogResult.OK;
            // 关闭窗体
            Close();
        }
        catch (Exception ex)
        {
            // 如果保存过程中出错，显示错误消息
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>
    /// 尝试从键名（例如 "Level_123"）解析出关卡 ID。
    /// </summary>
    /// <param name="key">关卡键名。</param>
    /// <returns>解析成功返回关卡 ID，否则返回 null。</returns>
    private static int? TryParseLevelId(string key)
    {
        // 查找第一个下划线的位置
        var idx = key.IndexOf('_');
        // 如果没有下划线，或者下划线在末尾，解析失败
        if (idx < 0 || idx >= key.Length - 1)
        {
            return null;
        }

        // 尝试解析下划线后的子字符串为整数
        return int.TryParse(key[(idx + 1)..], out var id) ? id : null;
    }

    /// <summary>
    /// 判断给定的键是否是关卡数据键（格式为 "Level_" 后跟数字）。
    /// </summary>
    /// <param name="key">要检查的键。</param>
    /// <returns>如果是关卡数据键则返回 true，否则返回 false。</returns>
    private static bool IsLevelDataKey(string? key)
    {
        // 使用正则表达式匹配 "Level_" 后跟一个或多个数字的模式
        return !string.IsNullOrWhiteSpace(key)
               && Regex.IsMatch(key, @"^Level_\d+$", RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// 判断给定的字段键是否应被视为布尔类型字段。
    /// </summary>
    /// <param name="fieldKey">字段键名。</param>
    /// <returns>如果是布尔类型字段则返回 true，否则返回 false。</returns>
    private static bool IsBoolField(string fieldKey)
    {
        // 通过模式匹配检查是否属于预定义的布尔字段列表
        return fieldKey is "Completed"
            or "Purchased"
            or "Revealed"
            or "ObjectivesCompleted"
            or "NGPEnabled"
            or "AssistModeEnabled"
            or "AssistModeCompleted"
            or "AssistModeClear";
    }

    /// <summary>
    /// 尝试将字符串解析为布尔值。
    /// </summary>
    /// <param name="raw">要解析的原始字符串。</param>
    /// <returns>解析成功返回布尔值，否则默认返回 false。</returns>
    private static bool TryParseBool(string raw)
    {
        // 首先尝试直接解析为布尔
        if (bool.TryParse(raw, out var b))
        {
            return b;
        }

        // 如果失败，尝试解析为整数，非零值视为 true
        return int.TryParse(raw, out var i) && i != 0;
    }

    /// <summary>
    /// 将内部 JSON 对象转换为键值对字典。
    /// </summary>
    /// <param name="inner">内部 JSON 对象。</param>
    /// <returns>返回键值对字典。</returns>
    private static Dictionary<string, JsonNode?> ToInnerMap(JsonObject inner)
    {
        var map = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        // 获取键数组和值数组
        var keyArray = inner["m_Key"] as JsonArray;
        var valueArray = inner["m_Value"] as JsonArray;
        // 如果任一数组缺失，则返回空字典
        if (keyArray is null || valueArray is null)
        {
            return map;
        }

        // 取两个数组长度的最小值进行遍历
        var count = Math.Min(keyArray.Count, valueArray.Count);
        for (var i = 0; i < count; i++)
        {
            // 获取键名，忽略空或空白的键
            var key = keyArray[i]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            // 将键和对应的值（深拷贝）添加到字典
            map[key] = valueArray[i]?.DeepClone();
        }

        return map;
    }

    /// <summary>
    /// 将键值对字典设置回内部 JSON 对象的 m_Key 和 m_Value 数组。
    /// </summary>
    /// <param name="inner">要更新的内部 JSON 对象。</param>
    /// <param name="map">包含键值对的字典。</param>
    private static void SetInnerMap(JsonObject inner, Dictionary<string, JsonNode?> map)
    {
        // 创建新的键数组和值数组
        var keyArray = new JsonArray();
        var valueArray = new JsonArray();
        // 遍历字典，将键值对分别添加到数组中
        foreach (var pair in map)
        {
            keyArray.Add(pair.Key);
            // 值需要深拷贝，防止修改影响原始引用
            valueArray.Add(pair.Value?.DeepClone());
        }

        // 将新数组设置回内部 JSON 对象
        inner["m_Key"] = keyArray;
        inner["m_Value"] = valueArray;
    }

    /// <summary>
    /// 尝试从字典中获取指定键的整数值。
    /// </summary>
    /// <param name="map">键值对字典。</param>
    /// <param name="key">要查找的键。</param>
    /// <returns>如果找到且可解析为整数则返回该值，否则返回 null。</returns>
    private static int? TryGetInt(Dictionary<string, JsonNode?> map, string key)
    {
        // 尝试获取节点，如果不存在或为 null，则返回 null
        if (!map.TryGetValue(key, out var node) || node is null)
        {
            return null;
        }

        // 尝试将节点值解析为整数
        return int.TryParse(node.ToString(), out var value) ? value : null;
    }

    /// <summary>
    /// 尝试从字典中获取指定键的布尔值。
    /// </summary>
    /// <param name="map">键值对字典。</param>
    /// <param name="key">要查找的键。</param>
    /// <returns>如果找到且可解析为布尔值则返回该值，否则返回 null。</returns>
    private static bool? TryGetBool(Dictionary<string, JsonNode?> map, string key)
    {
        // 尝试获取节点，如果不存在或为 null，则返回 null
        if (!map.TryGetValue(key, out var node) || node is null)
        {
            return null;
        }

        // 首先尝试解析为布尔值
        if (bool.TryParse(node.ToString(), out var b))
        {
            return b;
        }

        // 如果失败，尝试解析为整数，非零值视为 true
        if (int.TryParse(node.ToString(), out var i))
        {
            return i != 0;
        }

        // 解析失败，返回 null
        return null;
    }

    /// <summary>
    /// 表示一行关卡数据，包含原始键、内外部 JSON 对象、解析后的键值对映射和关卡 ID。
    /// </summary>
    private sealed class LevelRow
    {
        public required string LevelKey { get; init; }
        public required JsonObject Outer { get; init; }
        public required JsonObject Inner { get; init; }
        public required Dictionary<string, JsonNode?> Map { get; init; }
        public required int LevelId { get; init; }
    }

    /// <summary>
    /// 记录列的定义信息，包括键名、是否为布尔类型以及是否只读。
    /// </summary>
    /// <param name="Key">字段键名。</param>
    /// <param name="IsBool">是否为布尔类型。</param>
    /// <param name="ReadOnly">是否只读。</param>
    private sealed record ColumnDef(string Key, bool IsBool, bool ReadOnly);
}
