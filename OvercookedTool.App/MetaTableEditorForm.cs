using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using OvercookedTool.Core.Models;

namespace OvercookedTool.App;

/// <summary>
/// 用于编辑存档的 JSON 格式元数据 (Meta) 的窗口。
/// 提供了一个表格界面，用于查看和修改键值对，并支持将字段名翻译为中文显示。
/// </summary>
internal sealed class MetaTableEditorForm : Form
{
    // 用于显示和编辑键值对的表格控件
    private readonly DataGridView _table;
    // 控制是否启用翻译显示的复选框
    private readonly CheckBox _translateCheck;
    // 存储解析后的 JSON 根对象
    private readonly JsonObject _root;
    // 存储键名的 JSON 数组
    private readonly JsonArray _keys;
    // 存储值的 JSON 数组
    private readonly JsonArray _entries;
    // 将 JSON 数据解析后存储在内存中的行数据列表
    private readonly List<MetaRow> _rows = new();

    // [Browsable(false)] 和 [DesignerSerializationVisibility] 特性使此属性不在设计器中显示和序列化。
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string JsonText { get; private set; }

    // 构造函数：初始化窗体、解析 JSON 数据并构建用户界面。
    public MetaTableEditorForm(SaveFileEntry save, SaveVersion version, string jsonText, bool translateDefault = true)
    {
        // 解析传入的 JSON 文本，并获取必要的键和值数组。如果解析失败或缺少必要字段则抛出异常。
        _root = JsonNode.Parse(jsonText)?.AsObject() ?? throw new InvalidOperationException("Meta 内容不是有效 JSON。");
        _keys = _root["m_Keys"] as JsonArray ?? throw new InvalidOperationException("Meta 内容缺少 m_Keys。");
        _entries = _root["m_Entries"] as JsonArray ?? throw new InvalidOperationException("Meta 内容缺少 m_Entries。");
        JsonText = jsonText;

        // 设置窗体的基本属性。
        Text = $"修改存档元数据 - {save.FileName}";
        Width = 980;
        Height = 700;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(880, 580);

        // 创建主布局容器（TableLayoutPanel）并设置为三行单列。
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 第一行：头部信息，自动大小。
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // 第二行：表格主体，占据剩余所有空间。
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 第三行：按钮区域，自动大小。
        Controls.Add(root);

        // 创建头部布局，用于放置文件信息标签和翻译开关复选框。
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f)); // 第一列：信息标签，占满剩余空间。
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 第二列：复选框，自动大小。

        // 显示当前文件名、存档版本和条目数量的信息标签。
        var info = new Label
        {
            AutoSize = true,
            Text = $"文件: {save.FileName}  |  版本: {version}  |  条目: {Math.Min(_keys.Count, _entries.Count)}",
            Margin = new Padding(0, 6, 0, 6),
        };
        header.Controls.Add(info, 0, 0);

        // 创建翻译切换复选框，并绑定其状态改变事件。
        _translateCheck = new CheckBox
        {
            Text = "启用翻译显示",
            AutoSize = true,
            Checked = translateDefault,
            Margin = new Padding(8, 4, 0, 0),
        };
        // 当复选框状态改变时，刷新表格中键名的显示（翻译或原样）。
        _translateCheck.CheckedChanged += (_, _) => RefreshTranslation();
        header.Controls.Add(_translateCheck, 1, 0);
        root.Controls.Add(header, 0, 0);

        // 创建用于显示和编辑数据的 DataGridView 表格控件。
        _table = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false, // 不自动生成列，手动添加。
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false, // 隐藏行头（行号）。
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            EditMode = DataGridViewEditMode.EditOnEnter, // 单击单元格即进入编辑模式。
        };
        // 手动添加两列：键名列（只读）和值列（可编辑）。
        _table.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "key",
            HeaderText = GetMetaKeyHeader(), // 列头根据翻译设置动态获取。
            Width = 340,
            ReadOnly = true, // 键名列只读。
        });
        _table.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "value",
            HeaderText = GetMetaValueHeader(), // 列头根据翻译设置动态获取。
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, // 值列自动填充剩余宽度。
            ReadOnly = false, // 值列可编辑。
        });
        root.Controls.Add(_table, 0, 1);

        // 创建底部按钮面板，使用 FlowLayoutPanel 实现右对齐。
        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft, // 按钮从右向左排列。
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0),
        };
        // 创建“保存”按钮，点击后执行保存并关闭操作。
        var saveButton = new Button { Text = "保存", AutoSize = true };
        saveButton.Click += (_, _) => SaveAndClose();
        // 创建“取消”按钮，点击后直接关闭窗口并返回取消结果。
        var cancelButton = new Button { Text = "取消", AutoSize = true };
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        // 将按钮添加到面板。由于面板是右对齐，先添加的按钮会靠右。
        buttonRow.Controls.Add(saveButton);
        buttonRow.Controls.Add(cancelButton);
        root.Controls.Add(buttonRow, 0, 2);

        // 解析 JSON 数据为内存中的行对象列表，并填充到表格中显示。
        ParseRows();
        PopulateTable();
    }

    /// <summary>
    /// 解析 JSON 数组（_keys 和 _entries）并将其转换为内部使用的 MetaRow 对象列表。
    /// </summary>
    private void ParseRows()
    {
        _rows.Clear();
        // 条目数量取键数组和值数组长度的最小值，防止索引越界。
        var count = Math.Min(_keys.Count, _entries.Count);
        for (var i = 0; i < count; i++)
        {
            var key = _keys[i]?.GetValue<string>();
            // 跳过键名为空或空白的条目。
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var node = _entries[i];
            // 情况1：值是一个 JSON 对象，并且包含 "m_JSON" 字段。将其视为一个独立的 JSON 字段。
            if (node is JsonObject obj && obj["m_JSON"] is JsonValue)
            {
                _rows.Add(new MetaRow
                {
                    Index = i,
                    Key = key,
                    Value = obj["m_JSON"]?.GetValue<string>() ?? string.Empty,
                    EntryMode = MetaEntryMode.JsonField,
                    OriginalKind = JsonValueKind.String, // m_JSON 的值总是字符串。
                });
                continue;
            }

            // 情况2：值是一个基本的 JSON 值（字符串、数字、布尔值）。
            if (node is JsonValue primitive)
            {
                _rows.Add(new MetaRow
                {
                    Index = i,
                    Key = key,
                    // 将基本值转换为字符串表示，并去除可能的字符串引号。
                    Value = primitive.ToJsonString().Trim('"'),
                    EntryMode = MetaEntryMode.Primitive,
                    // 检测原始值的类型（数字、布尔、字符串），以便保存时还原。
                    OriginalKind = DetectKind(primitive),
                });
                continue;
            }

            // 情况3：其他复杂的 JSON 节点（如数组、嵌套对象），直接存储其原始 JSON 字符串。
            _rows.Add(new MetaRow
            {
                Index = i,
                Key = key,
                Value = node?.ToJsonString() ?? string.Empty,
                EntryMode = MetaEntryMode.RawNode,
                OriginalKind = JsonValueKind.Undefined, // 复杂节点没有简单的“原始类型”。
            });
        }
    }

    /// <summary>
    /// 根据解析好的 _rows 列表填充 DataGridView 表格的行。
    /// </summary>
    private void PopulateTable()
    {
        _table.Rows.Clear();
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var idx = _table.Rows.Add();
            var gridRow = _table.Rows[idx];
            // 第一列显示键名（可能经过翻译处理）。
            gridRow.Cells[0].Value = GetDisplayKey(row.Key);
            // 将原始键名存储在单元格的 Tag 属性中，以便后续获取。
            gridRow.Cells[0].Tag = row.Key;
            // 第二列显示值（可编辑的字符串）。
            gridRow.Cells[1].Value = row.Value;
        }
    }

    /// <summary>
    /// 刷新表格的显示内容，主要用于响应翻译开关状态的变化。
    /// 更新列头文本和所有行中键名的显示。
    /// </summary>
    private void RefreshTranslation()
    {
        // 更新两列的表头文本（翻译或原样）。
        _table.Columns[0].HeaderText = GetMetaKeyHeader();
        _table.Columns[1].HeaderText = GetMetaValueHeader();

        // 遍历表格所有行，重新设置键名的显示文本。
        for (var i = 0; i < _table.Rows.Count; i++)
        {
            // 从单元格的 Tag 中获取原始键名，如果 Tag 为空则从内部数据行获取。
            var key = _table.Rows[i].Cells[0].Tag as string ?? _rows[i].Key;
            _table.Rows[i].Cells[0].Value = GetDisplayKey(key);
        }
    }

    /// <summary>
    /// 根据翻译开关状态，返回用于显示的键名。
    /// </summary>
    /// <param name="key">原始键名。</param>
    /// <returns>翻译后的显示名称或原始键名。</returns>
    private string GetDisplayKey(string key)
    {
        return SaveDisplayConfig.Instance.GetFieldDisplayName(key, _translateCheck.Checked);
    }

    /// <summary>
    /// 获取键名列的表头文本，根据翻译开关决定是否翻译。
    /// </summary>
    private string GetMetaKeyHeader()
    {
        return _translateCheck.Checked
            ? SaveDisplayConfig.Instance.GetUiTranslation("MetaKeyHeader", "字段")
            : "Key";
    }

    /// <summary>
    /// 获取值列的表头文本，根据翻译开关决定是否翻译。
    /// </summary>
    private string GetMetaValueHeader()
    {
        return _translateCheck.Checked
            ? SaveDisplayConfig.Instance.GetUiTranslation("MetaValueHeader", "值")
            : "Value";
    }

    /// <summary>
    /// 将表格中的修改保存回 JSON 结构，并关闭窗体。
    /// </summary>
    private void SaveAndClose()
    {
        try
        {
            // 确保当前正在编辑的单元格提交更改。
            _table.EndEdit();
            // 遍历所有行，将表格中修改的值写回到对应的 JSON 数组中。
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                // 从表格单元格获取用户输入的值。
                var value = Convert.ToString(_table.Rows[i].Cells[1].Value) ?? string.Empty;

                // 根据条目的原始模式，以不同方式更新 JSON 数组。
                switch (row.EntryMode)
                {
                    case MetaEntryMode.JsonField:
                    {
                        // 对于 JsonField 模式，更新 JSON 对象中 "m_JSON" 字段的值。
                        if (_entries[row.Index] is JsonObject obj)
                        {
                            obj["m_JSON"] = value;
                        }
                        break;
                    }
                    case MetaEntryMode.Primitive:
                    {
                        // 对于基本类型模式，根据原始类型（数字、布尔、字符串）重新构建 JSON 值。
                        _entries[row.Index] = BuildPrimitive(value, row.OriginalKind);
                        break;
                    }
                    default:
                    {
                        // 对于原始节点模式，直接将字符串值赋给数组元素。
                        // 注意：这可能会改变节点的类型（从复杂对象变为字符串）。
                        _entries[row.Index] = value;
                        break;
                    }
                }
            }

            // 将修改后的整个 JSON 对象序列化为紧凑格式的字符串，并更新 JsonText 属性。
            JsonText = _root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            // 如果保存过程中发生任何异常，向用户显示错误信息。
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>
    /// 根据指定的 JSON 值类型，将字符串值转换为对应的 JsonNode（基本值）。
    /// </summary>
    /// <param name="value">字符串形式的值。</param>
    /// <param name="kind">目标 JSON 值类型。</param>
    /// <returns>创建的 JsonValue 节点。</returns>
    private static JsonNode BuildPrimitive(string value, JsonValueKind kind)
    {
        // 使用模式匹配，尝试将字符串解析为指定类型，失败则作为字符串处理。
        return kind switch
        {
            JsonValueKind.Number when long.TryParse(value, out var i) => JsonValue.Create(i)!,
            JsonValueKind.Number when double.TryParse(value, out var d) => JsonValue.Create(d)!,
            JsonValueKind.True or JsonValueKind.False when bool.TryParse(value, out var b) => JsonValue.Create(b)!,
            _ => JsonValue.Create(value)!,
        };
    }

    /// <summary>
    /// 检测一个 JsonValue 的实际数据类型（True, False, Number, String）。
    /// 用于在 ParseRows 中记录基本值的原始类型。
    /// </summary>
    /// <param name="value">要检测的 JsonValue。</param>
    /// <returns>检测到的 JsonValueKind。</returns>
    private static JsonValueKind DetectKind(JsonValue value)
    {
        // 将值序列化为 JSON 字符串以便判断。
        var json = value.ToJsonString();
        if (json == "true")
        {
            return JsonValueKind.True;
        }

        if (json == "false")
        {
            return JsonValueKind.False;
        }

        // 如果字符串不以引号开头，且长度大于0，则可能是数字（如 123, 45.67）。
        // 注意：这是一个简化的检测，对于带引号的数字字符串可能不准确。
        if (json.Length > 0 && json[0] != '"')
        {
            return JsonValueKind.Number;
        }

        // 默认视为字符串。
        return JsonValueKind.String;
    }

    /// <summary>
    /// 表示元数据条目的解析模式。
    /// </summary>
    private enum MetaEntryMode
    {
        /// <summary>值是一个包含 "m_JSON" 字段的 JSON 对象。</summary>
        JsonField,
        /// <summary>值是一个基本的 JSON 值（字符串、数字、布尔值）。</summary>
        Primitive,
        /// <summary>值是一个复杂的 JSON 节点（数组、对象等）。</summary>
        RawNode,
    }

    /// <summary>
    /// 表示从 JSON 数据中解析出的一行元数据。
    /// </summary>
    private sealed class MetaRow
    {
        /// <summary>原始 JSON 数组中的索引位置。</summary>
        public required int Index { get; init; }
        /// <summary>条目的键名。</summary>
        public required string Key { get; init; }
        /// <summary>条目的值（以字符串形式存储）。</summary>
        public required string Value { get; init; }
        /// <summary>条目在 JSON 中的解析模式。</summary>
        public required MetaEntryMode EntryMode { get; init; }
        /// <summary>原始值的 JSON 类型（用于基本类型模式）。</summary>
        public required JsonValueKind OriginalKind { get; init; }
    }
}
