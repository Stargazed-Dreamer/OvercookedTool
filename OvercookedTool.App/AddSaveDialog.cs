namespace OvercookedTool.App;

internal sealed class AddSaveDialog : Form
{
    private readonly NumericUpDown _slotInput;
    private readonly TextBox _dlcInput;
    private readonly ComboBox _presetCombo;

    public int Slot => (int)_slotInput.Value;
    public int? DlcId => int.TryParse(_dlcInput.Text.Trim(), out var v) ? v : null;
    public string Preset => _presetCombo.SelectedItem?.ToString() ?? "空存档";

    public AddSaveDialog(int suggestedSlot, int? suggestedDlc)
    {
        Text = "添加存档";
        Width = 420;
        Height = 240;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(14),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(new Label { Text = "档位:", AutoSize = true, Margin = new Padding(0, 7, 8, 0) }, 0, 0);
        _slotInput = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 999,
            Value = Math.Max(0, suggestedSlot),
            Width = 120,
        };
        root.Controls.Add(_slotInput, 1, 0);

        root.Controls.Add(new Label { Text = "DLC编号(可空):", AutoSize = true, Margin = new Padding(0, 7, 8, 0) }, 0, 1);
        _dlcInput = new TextBox
        {
            Text = suggestedDlc?.ToString() ?? string.Empty,
            Width = 120,
        };
        root.Controls.Add(_dlcInput, 1, 1);

        root.Controls.Add(new Label { Text = "预设:", AutoSize = true, Margin = new Padding(0, 7, 8, 0) }, 0, 2);
        _presetCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 180,
        };
        _presetCombo.Items.AddRange(new object[] { "空存档", "通关存档" });
        _presetCombo.SelectedIndex = 0;
        root.Controls.Add(_presetCombo, 1, 2);

        var actions = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            AutoSize = true,
        };
        var ok = new Button { Text = "创建", AutoSize = true };
        ok.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };
        var cancel = new Button { Text = "取消", AutoSize = true };
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        actions.Controls.Add(ok);
        actions.Controls.Add(cancel);
        root.Controls.Add(actions, 1, 3);
    }
}

