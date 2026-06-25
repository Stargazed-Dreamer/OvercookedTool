namespace OvercookedTool.App;

/// <summary>
/// 设置窗体，用于应用程序的各项配置管理
/// </summary>
internal sealed class SettingsForm : Form
{
    private readonly CheckBox _autoDetectCheck;    // 导入时自动检测路径复选框
    private readonly CheckBox _loggingCheck;       // 启用日志记录复选框
    private readonly NumericUpDown _maxHistory;    // 最近历史保留条数数值选择框
    private readonly NumericUpDown _maxBackup;     // 每个存档保留备份数数值选择框

    public bool EnableAutoDetectOnImport => _autoDetectCheck.Checked;    // 获取导入时是否自动检测路径的设置值
    public bool EnableLogging => _loggingCheck.Checked;                  // 获取是否启用日志记录的设置值
    public int MaxRecentCount => (int)_maxHistory.Value;                 // 获取最大历史记录条数的设置值
    public int MaxBackupPerSave => (int)_maxBackup.Value;                // 获取每个存档最大备份数的设置值

    public SettingsForm(AppSettings settings)
    {
        // 设置窗体基本属性
        Text = "设置";
        Width = 560;
        Height = 360;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        MinimumSize = new Size(520, 320);
        BackColor = Color.FromArgb(248, 251, 255);

        // 创建主容器布局
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

        // 添加标题标签
        var title = new Label
        {
            Text = "通用设置",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10),
        };
        root.Controls.Add(title, 0, 0);

        // 创建设置卡片容器
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

        // 初始化自动检测路径复选框
        _autoDetectCheck = new CheckBox
        {
            Text = "打开导入窗口时自动检测候选路径",
            Checked = settings.EnableAutoDetectOnImport,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
        };
        card.Controls.Add(_autoDetectCheck, 0, 0);

        // 初始化日志记录复选框
        _loggingCheck = new CheckBox
        {
            Text = "启用日志记录",
            Checked = settings.EnableLogging,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
        };
        card.Controls.Add(_loggingCheck, 0, 1);

        // 创建历史记录设置行
        var recentRow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 2, 0, 8),
        };
        recentRow.Controls.Add(new Label { Text = "最近历史保留条数:", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
        // 初始化历史记录条数数值选择框，限制在5-100范围内
        _maxHistory = new NumericUpDown
        {
            Minimum = 5,
            Maximum = 100,
            Value = Math.Clamp(settings.MaxRecentCount, 5, 100),
            Width = 90,
        };
        recentRow.Controls.Add(_maxHistory);
        card.Controls.Add(recentRow, 0, 2);

        // 创建备份设置行
        var backupRow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 2, 0, 0),
        };
        backupRow.Controls.Add(new Label { Text = "每个存档保留备份数:", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
        // 初始化备份数数值选择框，限制在1-50范围内
        _maxBackup = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 50,
            Value = Math.Clamp(settings.MaxBackupPerSave, 1, 50),
            Width = 90,
        };
        backupRow.Controls.Add(_maxBackup);
        // 添加默认值提示标签
        backupRow.Controls.Add(new Label { Text = "(默认10)", AutoSize = true, Margin = new Padding(8, 8, 0, 0), ForeColor = Color.FromArgb(96, 96, 96) });
        card.Controls.Add(backupRow, 0, 3);

        // 将设置卡片添加到主容器
        root.Controls.Add(card, 0, 1);

        // 添加说明提示标签
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

        // 创建按钮行容器，使用右对齐布局
        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
        };
        // 创建保存按钮，设置样式和事件处理
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
            DialogResult = DialogResult.OK;   // 设置对话框结果为确认
            Close();                          // 关闭窗体
        };

        // 创建取消按钮
        var cancel = new Button
        {
            Text = "取消",
            AutoSize = true,
        };
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;  // 设置对话框结果为取消
            Close();                             // 关闭窗体
        };

        // 将按钮添加到按钮行，并将按钮行添加到主容器
        buttonRow.Controls.Add(ok);
        buttonRow.Controls.Add(cancel);
        root.Controls.Add(buttonRow, 0, 3);
    }
}
