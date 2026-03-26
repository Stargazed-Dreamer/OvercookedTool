using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using OvercookedTool.Core.Models;

namespace OvercookedTool.App;

internal sealed class MetaTableEditorForm : Form
{
    private readonly DataGridView _table;
    private readonly CheckBox _translateCheck;
    private readonly JsonObject _root;
    private readonly JsonArray _keys;
    private readonly JsonArray _entries;
    private readonly List<MetaRow> _rows = new();

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string JsonText { get; private set; }

    public MetaTableEditorForm(SaveFileEntry save, SaveVersion version, string jsonText, bool translateDefault = true)
    {
        _root = JsonNode.Parse(jsonText)?.AsObject() ?? throw new InvalidOperationException("Meta 内容不是有效 JSON。");
        _keys = _root["m_Keys"] as JsonArray ?? throw new InvalidOperationException("Meta 内容缺少 m_Keys。");
        _entries = _root["m_Entries"] as JsonArray ?? throw new InvalidOperationException("Meta 内容缺少 m_Entries。");
        JsonText = jsonText;

        Text = $"修改存档元数据 - {save.FileName}";
        Width = 980;
        Height = 700;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(880, 580);

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

        var info = new Label
        {
            AutoSize = true,
            Text = $"文件: {save.FileName}  |  版本: {version}  |  条目: {Math.Min(_keys.Count, _entries.Count)}",
            Margin = new Padding(0, 6, 0, 6),
        };
        header.Controls.Add(info, 0, 0);

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
        _table.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "key",
            HeaderText = GetMetaKeyHeader(),
            Width = 340,
            ReadOnly = true,
        });
        _table.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "value",
            HeaderText = GetMetaValueHeader(),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            ReadOnly = false,
        });
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

        ParseRows();
        PopulateTable();
    }

    private void ParseRows()
    {
        _rows.Clear();
        var count = Math.Min(_keys.Count, _entries.Count);
        for (var i = 0; i < count; i++)
        {
            var key = _keys[i]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var node = _entries[i];
            if (node is JsonObject obj && obj["m_JSON"] is JsonValue)
            {
                _rows.Add(new MetaRow
                {
                    Index = i,
                    Key = key,
                    Value = obj["m_JSON"]?.GetValue<string>() ?? string.Empty,
                    EntryMode = MetaEntryMode.JsonField,
                    OriginalKind = JsonValueKind.String,
                });
                continue;
            }

            if (node is JsonValue primitive)
            {
                _rows.Add(new MetaRow
                {
                    Index = i,
                    Key = key,
                    Value = primitive.ToJsonString().Trim('"'),
                    EntryMode = MetaEntryMode.Primitive,
                    OriginalKind = DetectKind(primitive),
                });
                continue;
            }

            _rows.Add(new MetaRow
            {
                Index = i,
                Key = key,
                Value = node?.ToJsonString() ?? string.Empty,
                EntryMode = MetaEntryMode.RawNode,
                OriginalKind = JsonValueKind.Undefined,
            });
        }
    }

    private void PopulateTable()
    {
        _table.Rows.Clear();
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var idx = _table.Rows.Add();
            var gridRow = _table.Rows[idx];
            gridRow.Cells[0].Value = GetDisplayKey(row.Key);
            gridRow.Cells[0].Tag = row.Key;
            gridRow.Cells[1].Value = row.Value;
        }
    }

    private void RefreshTranslation()
    {
        _table.Columns[0].HeaderText = GetMetaKeyHeader();
        _table.Columns[1].HeaderText = GetMetaValueHeader();

        for (var i = 0; i < _table.Rows.Count; i++)
        {
            var key = _table.Rows[i].Cells[0].Tag as string ?? _rows[i].Key;
            _table.Rows[i].Cells[0].Value = GetDisplayKey(key);
        }
    }

    private string GetDisplayKey(string key)
    {
        return SaveDisplayConfig.Instance.GetFieldDisplayName(key, _translateCheck.Checked);
    }

    private string GetMetaKeyHeader()
    {
        return _translateCheck.Checked
            ? SaveDisplayConfig.Instance.GetUiTranslation("MetaKeyHeader", "字段")
            : "Key";
    }

    private string GetMetaValueHeader()
    {
        return _translateCheck.Checked
            ? SaveDisplayConfig.Instance.GetUiTranslation("MetaValueHeader", "值")
            : "Value";
    }

    private void SaveAndClose()
    {
        try
        {
            _table.EndEdit();
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var value = Convert.ToString(_table.Rows[i].Cells[1].Value) ?? string.Empty;

                switch (row.EntryMode)
                {
                    case MetaEntryMode.JsonField:
                    {
                        if (_entries[row.Index] is JsonObject obj)
                        {
                            obj["m_JSON"] = value;
                        }
                        break;
                    }
                    case MetaEntryMode.Primitive:
                    {
                        _entries[row.Index] = BuildPrimitive(value, row.OriginalKind);
                        break;
                    }
                    default:
                    {
                        _entries[row.Index] = value;
                        break;
                    }
                }
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

    private static JsonNode BuildPrimitive(string value, JsonValueKind kind)
    {
        return kind switch
        {
            JsonValueKind.Number when long.TryParse(value, out var i) => JsonValue.Create(i)!,
            JsonValueKind.Number when double.TryParse(value, out var d) => JsonValue.Create(d)!,
            JsonValueKind.True or JsonValueKind.False when bool.TryParse(value, out var b) => JsonValue.Create(b)!,
            _ => JsonValue.Create(value)!,
        };
    }

    private static JsonValueKind DetectKind(JsonValue value)
    {
        var json = value.ToJsonString();
        if (json == "true")
        {
            return JsonValueKind.True;
        }

        if (json == "false")
        {
            return JsonValueKind.False;
        }

        if (json.Length > 0 && json[0] != '"')
        {
            return JsonValueKind.Number;
        }

        return JsonValueKind.String;
    }

    private enum MetaEntryMode
    {
        JsonField,
        Primitive,
        RawNode,
    }

    private sealed class MetaRow
    {
        public required int Index { get; init; }
        public required string Key { get; init; }
        public required string Value { get; init; }
        public required MetaEntryMode EntryMode { get; init; }
        public required JsonValueKind OriginalKind { get; init; }
    }
}
