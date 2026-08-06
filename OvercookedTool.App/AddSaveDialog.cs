namespace OvercookedTool.App;

/// <summary>
/// 这是一个用于添加存档的对话框类，继承自Form，提供用户输入存档档位、DLC编号和预设选项的界面。
/// </summary>
internal sealed class AddSaveDialog : Form
{
    private readonly NumericUpDown _slotInput; // 用于输入存档档位的数值输入框
    private readonly TextBox _dlcInput; // 用于输入DLC编号的文本框
    private readonly ComboBox _presetCombo; // 用于选择预设的下拉框

    public int Slot => (int)_slotInput.Value; // 返回用户输入的存档档位值
    public int? DlcId => int.TryParse(_dlcInput.Text.Trim(), out var v) ? v : null; // 尝试将DLC输入文本解析为整数，成功则返回整数，否则返回null
    public string Preset => _presetCombo.SelectedItem?.ToString() ?? "空存档"; // 返回选中的预设名称，如果未选中则默认返回"空存档"

    public AddSaveDialog(int suggestedSlot, int? suggestedDlc)
    {
        // 初始化对话框的基本属性
        Text = "添加存档";
        Width = 420;
        Height = 240;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        // 创建根布局控件TableLayoutPanel，用于组织界面元素
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(14),
        };
        // 设置列样式：第一列自适应内容，第二列填充剩余空间
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // 设置行样式：前三行自适应内容，第四行填充剩余空间
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        // 添加档位标签和输入框
        root.Controls.Add(new Label { Text = "档位:", AutoSize = true, Margin = new Padding(0, 7, 8, 0) }, 0, 0);
        _slotInput = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 999,
            Value = Math.Max(0, suggestedSlot), // 使用建议值，确保不小于0
            Width = 120,
        };
        root.Controls.Add(_slotInput, 1, 0);

        // 添加DLC编号标签和输入框
        root.Controls.Add(new Label { Text = "DLC编号(可空):", AutoSize = true, Margin = new Padding(0, 7, 8, 0) }, 0, 1);
        _dlcInput = new TextBox
        {
            Text = suggestedDlc?.ToString() ?? string.Empty, // 如果建议DLC为null，则显示空字符串
            Width = 120,
        };
        root.Controls.Add(_dlcInput, 1, 1);

        // 添加预设标签和下拉框
        root.Controls.Add(new Label { Text = "预设:", AutoSize = true, Margin = new Padding(0, 7, 8, 0) }, 0, 2);
        _presetCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList, // 设置为只读下拉列表
            Width = 180,
        };
        // 添加预设选项
        _presetCombo.Items.AddRange(new object[] { "空存档", "通关存档" });
        _presetCombo.SelectedIndex = 0; // 默认选中第一个选项
        root.Controls.Add(_presetCombo, 1, 2);

        // 创建操作按钮面板，使用FlowLayoutPanel并右对齐按钮
        var actions = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            AutoSize = true,
        };
        // 创建确定按钮，点击后设置DialogResult为OK并关闭对话框
        var ok = new Button { Text = "创建", AutoSize = true };
        ok.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };
        // 创建取消按钮，点击后设置DialogResult为Cancel并关闭对话框
        var cancel = new Button { Text = "取消", AutoSize = true };
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        // 将按钮添加到操作面板
        actions.Controls.Add(ok);
        actions.Controls.Add(cancel);
        // 将操作面板添加到根布局的第四行第二列
        root.Controls.Add(actions, 1, 3);
    }
}

