using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OvercookedTool.Core.Models;

namespace OvercookedTool.App;

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

    public SaveTableEditorForm(SaveFileEntry save, SaveVersion version, string jsonText, bool translateDefault = true)
    {
        _config = SaveDisplayConfig.Instance;
        _groupKey = _config.GetGroupKey(save);

        Text = $"编辑存档 - {save.FileName}";
        Width = 980;
        Height = 720;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 620);

        (_root, _rows) = ParseLevels(jsonText);
        JsonText = jsonText;

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

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var infoLabel = new Label
        {
            AutoSize = true,
            Text = $"文件: {save.FileName}  |  版本: {version}  |  分组: {_config.GetGroupDisplayName(_groupKey)} ({_groupKey})  |  关卡记录: {_rows.Count}",
            Margin = new Padding(0, 6, 0, 6),
        };
        header.Controls.Add(infoLabel, 0, 0);

        _translateCheck = new CheckBox
        {
            Text = "启用翻译显示",
            AutoSize = true,
            Checked = translateDefault,
            Margin = new Padding(8, 4, 0, 0),
        };
        _translateCheck.CheckedChanged += (_, _) => RefreshTranslation();
        header.Controls.Add(_translateCheck, 1, 0);
        root.Controls.Add(header, 0, 0);

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

        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0),
        };
        var saveButton = new Button { Text = "保存", AutoSize = true };
        saveButton.Click += (_, _) => SaveAndClose();
        var cancelButton = new Button { Text = "取消", AutoSize = true };
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        buttonRow.Controls.Add(saveButton);
        buttonRow.Controls.Add(cancelButton);
        root.Controls.Add(buttonRow, 0, 2);

        BuildColumns();
        PopulateTable();
    }

    private static (JsonObject Root, List<LevelRow> Rows) ParseLevels(string jsonText)
    {
        var root = JsonNode.Parse(jsonText)?.AsObject()
                   ?? throw new InvalidOperationException("存档内容不是有效 JSON。");
        var keys = root["m_Keys"] as JsonArray
                   ?? throw new InvalidOperationException("存档缺少 m_Keys。");
        var entries = root["m_Entries"] as JsonArray
                      ?? throw new InvalidOperationException("存档缺少 m_Entries。");

        var count = Math.Min(keys.Count, entries.Count);
        var rows = new List<LevelRow>();
        for (var i = 0; i < count; i++)
        {
            var key = keys[i]?.GetValue<string>();
            if (!IsLevelDataKey(key))
            {
                continue;
            }

            if (entries[i] is not JsonObject outer)
            {
                continue;
            }

            var innerText = outer["m_JSON"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(innerText))
            {
                continue;
            }

            var inner = JsonNode.Parse(innerText)?.AsObject();
            if (inner is null)
            {
                continue;
            }

            var map = ToInnerMap(inner);
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

        return (root, rows.OrderBy(x => x.LevelId).ThenBy(x => x.LevelKey).ToList());
    }

    private void BuildColumns()
    {
        _table.Columns.Clear();
        _fieldColumns.Clear();
        _fieldColumnIndex.Clear();

        _table.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "level_display",
            HeaderText = GetLevelHeaderText(),
            ReadOnly = true,
            Width = 220,
        });

        var preferredOrder = new[]
        {
            "LevelID", "ScoreStars", "HighScore", "Completed", "Purchased", "Revealed",
            "ObjectivesCompleted", "AssistModeCompleted", "AssistModeEnabled", "NGPEnabled",
            "FailedAttempts", "SurvivalModeTime",
        };
        var orderMap = preferredOrder
            .Select((key, idx) => (key, idx))
            .ToDictionary(x => x.key, x => x.idx, StringComparer.Ordinal);

        var keys = _rows
            .SelectMany(x => x.Map.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => orderMap.TryGetValue(x, out var idx) ? idx : int.MaxValue)
            .ThenBy(x => x, StringComparer.Ordinal)
            .ToList();

        foreach (var key in keys)
        {
            var isBool = IsBoolField(key);
            DataGridViewColumn column = isBool
                ? new DataGridViewCheckBoxColumn()
                : new DataGridViewTextBoxColumn();
            column.Name = key;
            column.HeaderText = _config.GetFieldDisplayName(key, translated: _translateCheck.Checked);
            column.ReadOnly = string.Equals(key, "LevelID", StringComparison.Ordinal);
            column.Width = key switch
            {
                "ScoreStars" => 75,
                "HighScore" => 90,
                _ => isBool ? 92 : 110,
            };
            _table.Columns.Add(column);
            _fieldColumns.Add(new ColumnDef(key, isBool, column.ReadOnly));
            _fieldColumnIndex[key] = _table.Columns.Count - 1;
        }
    }

    private void PopulateTable()
    {
        _table.Rows.Clear();
        foreach (var row in _rows)
        {
            var rowIndex = _table.Rows.Add();
            var gridRow = _table.Rows[rowIndex];
            gridRow.Cells[0].Value = GetDisplayLevel(row);
            foreach (var field in _fieldColumns)
            {
                var columnIndex = _fieldColumnIndex[field.Key];
                if (!row.Map.TryGetValue(field.Key, out var value) || value is null)
                {
                    gridRow.Cells[columnIndex].Value = field.IsBool ? false : string.Empty;
                    continue;
                }

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

    private void RefreshTranslation()
    {
        _table.Columns[0].HeaderText = GetLevelHeaderText();
        foreach (var field in _fieldColumns)
        {
            if (_fieldColumnIndex.TryGetValue(field.Key, out var index))
            {
                _table.Columns[index].HeaderText = _config.GetFieldDisplayName(field.Key, _translateCheck.Checked);
            }
        }

        for (var i = 0; i < _rows.Count && i < _table.Rows.Count; i++)
        {
            _table.Rows[i].Cells[0].Value = GetDisplayLevel(_rows[i]);
        }
    }

    private string GetDisplayLevel(LevelRow row)
    {
        if (!_translateCheck.Checked)
        {
            return row.LevelKey;
        }

        var fallback = $"Level_{row.LevelId}";
        return _config.GetLevelDisplayName(_groupKey, row.LevelId, fallback);
    }

    private string GetLevelHeaderText()
    {
        return _translateCheck.Checked
            ? _config.GetUiTranslation("LevelHeader", "关卡")
            : "Level";
    }

    private void SaveAndClose()
    {
        try
        {
            _table.EndEdit();
            for (var i = 0; i < _rows.Count; i++)
            {
                var ui = _table.Rows[i];
                var model = _rows[i];

                foreach (var field in _fieldColumns)
                {
                    if (!_fieldColumnIndex.TryGetValue(field.Key, out var columnIndex))
                    {
                        continue;
                    }

                    if (field.ReadOnly)
                    {
                        continue;
                    }

                    if (field.IsBool)
                    {
                        var b = Convert.ToBoolean(ui.Cells[columnIndex].Value ?? false);
                        model.Map[field.Key] = b ? "True" : "False";
                        continue;
                    }

                    var raw = Convert.ToString(ui.Cells[columnIndex].Value)?.Trim() ?? string.Empty;
                    if (string.Equals(field.Key, "ScoreStars", StringComparison.Ordinal) && int.TryParse(raw, out var stars))
                    {
                        raw = Math.Clamp(stars, 0, 4).ToString();
                    }
                    else if ((string.Equals(field.Key, "HighScore", StringComparison.Ordinal)
                           || string.Equals(field.Key, "FailedAttempts", StringComparison.Ordinal))
                           && !int.TryParse(raw, out _))
                    {
                        throw new InvalidOperationException($"第 {i + 1} 行字段 {field.Key} 需要数字。");
                    }

                    model.Map[field.Key] = raw;
                }

                SetInnerMap(model.Inner, model.Map);
                model.Outer["m_JSON"] = model.Inner.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
            }

            JsonText = _root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static int? TryParseLevelId(string key)
    {
        var idx = key.IndexOf('_');
        if (idx < 0 || idx >= key.Length - 1)
        {
            return null;
        }

        return int.TryParse(key[(idx + 1)..], out var id) ? id : null;
    }

    private static bool IsLevelDataKey(string? key)
    {
        return !string.IsNullOrWhiteSpace(key)
               && Regex.IsMatch(key, @"^Level_\d+$", RegexOptions.CultureInvariant);
    }

    private static bool IsBoolField(string fieldKey)
    {
        return fieldKey is "Completed"
            or "Purchased"
            or "Revealed"
            or "ObjectivesCompleted"
            or "NGPEnabled"
            or "AssistModeEnabled"
            or "AssistModeCompleted"
            or "AssistModeClear";
    }

    private static bool TryParseBool(string raw)
    {
        if (bool.TryParse(raw, out var b))
        {
            return b;
        }

        return int.TryParse(raw, out var i) && i != 0;
    }

    private static Dictionary<string, JsonNode?> ToInnerMap(JsonObject inner)
    {
        var map = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        var keyArray = inner["m_Key"] as JsonArray;
        var valueArray = inner["m_Value"] as JsonArray;
        if (keyArray is null || valueArray is null)
        {
            return map;
        }

        var count = Math.Min(keyArray.Count, valueArray.Count);
        for (var i = 0; i < count; i++)
        {
            var key = keyArray[i]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            map[key] = valueArray[i]?.DeepClone();
        }

        return map;
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

    private static int? TryGetInt(Dictionary<string, JsonNode?> map, string key)
    {
        if (!map.TryGetValue(key, out var node) || node is null)
        {
            return null;
        }

        return int.TryParse(node.ToString(), out var value) ? value : null;
    }

    private static bool? TryGetBool(Dictionary<string, JsonNode?> map, string key)
    {
        if (!map.TryGetValue(key, out var node) || node is null)
        {
            return null;
        }

        if (bool.TryParse(node.ToString(), out var b))
        {
            return b;
        }

        if (int.TryParse(node.ToString(), out var i))
        {
            return i != 0;
        }

        return null;
    }

    private sealed class LevelRow
    {
        public required string LevelKey { get; init; }
        public required JsonObject Outer { get; init; }
        public required JsonObject Inner { get; init; }
        public required Dictionary<string, JsonNode?> Map { get; init; }
        public required int LevelId { get; init; }
    }

    private sealed record ColumnDef(string Key, bool IsBool, bool ReadOnly);
}
