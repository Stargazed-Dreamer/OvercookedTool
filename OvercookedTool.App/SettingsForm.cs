namespace OvercookedTool.App;

internal sealed class SettingsForm : Form
{
    private readonly CheckBox _autoDetectCheck;
    private readonly CheckBox _loggingCheck;
    private readonly NumericUpDown _maxHistory;
    private readonly NumericUpDown _maxBackup;

    public bool EnableAutoDetectOnImport => _autoDetectCheck.Checked;
    public bool EnableLogging => _loggingCheck.Checked;
    public int MaxRecentCount => (int)_maxHistory.Value;
    public int MaxBackupPerSave => (int)_maxBackup.Value;

    public SettingsForm(AppSettings settings)
    {
        Text = "设置";
        Width = 560;
        Height = 360;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        MinimumSize = new Size(520, 320);
        BackColor = Color.FromArgb(248, 251, 255);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16),
            BackColor = BackColor,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var title = new Label
        {
            Text = "通用设置",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10),
        };
        root.Controls.Add(title, 0, 0);

        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            Padding = new Padding(12),
            BackColor = Color.White,
            Margin = new Padding(0),
        };
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _autoDetectCheck = new CheckBox
        {
            Text = "打开导入窗口时自动检测候选路径",
            Checked = settings.EnableAutoDetectOnImport,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
        };
        card.Controls.Add(_autoDetectCheck, 0, 0);

        _loggingCheck = new CheckBox
        {
            Text = "启用日志记录",
            Checked = settings.EnableLogging,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
        };
        card.Controls.Add(_loggingCheck, 0, 1);

        var recentRow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 2, 0, 8),
        };
        recentRow.Controls.Add(new Label { Text = "最近历史保留条数:", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
        _maxHistory = new NumericUpDown
        {
            Minimum = 5,
            Maximum = 100,
            Value = Math.Clamp(settings.MaxRecentCount, 5, 100),
            Width = 90,
        };
        recentRow.Controls.Add(_maxHistory);
        card.Controls.Add(recentRow, 0, 2);

        var backupRow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 2, 0, 0),
        };
        backupRow.Controls.Add(new Label { Text = "每个存档保留备份数:", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
        _maxBackup = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 50,
            Value = Math.Clamp(settings.MaxBackupPerSave, 1, 50),
            Width = 90,
        };
        backupRow.Controls.Add(_maxBackup);
        backupRow.Controls.Add(new Label { Text = "(默认10)", AutoSize = true, Margin = new Padding(8, 8, 0, 0), ForeColor = Color.FromArgb(96, 96, 96) });
        card.Controls.Add(backupRow, 0, 3);

        root.Controls.Add(card, 0, 1);

        var hint = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            MaximumSize = new Size(1000, 0),
            Text = "日志关闭后将不再写入 logs 目录。备份保留数会应用到后续新增的备份。",
            ForeColor = Color.FromArgb(92, 92, 92),
            Margin = new Padding(0, 10, 0, 8),
        };
        root.Controls.Add(hint, 0, 2);

        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
        };
        var ok = new Button
        {
            Text = "保存",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(33, 150, 243),
            ForeColor = Color.White,
        };
        ok.FlatAppearance.BorderSize = 0;
        ok.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };

        var cancel = new Button
        {
            Text = "取消",
            AutoSize = true,
        };
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        buttonRow.Controls.Add(ok);
        buttonRow.Controls.Add(cancel);
        root.Controls.Add(buttonRow, 0, 3);
    }
}
