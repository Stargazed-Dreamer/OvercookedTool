using System.ComponentModel;

namespace OvercookedTool.App;

/// <summary>
/// 用于让用户从候选列表中选择一个包路径的对话框。
/// </summary>
internal sealed class SelectPackageDialog : Form
{
    /// <summary>
    /// 用于显示候选包列表的列表框控件。
    /// </summary>
    private readonly ListBox _list;

    /// <summary>
    /// 用户选中的包路径，若未选择则为 null。
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? SelectedPath { get; private set; }

    /// <summary>
    /// 初始化选择包对话框。
    /// </summary>
    /// <param name="title">对话框标题</param>
    /// <param name="description">对话框描述文本</param>
    /// <param name="candidates">候选包路径的只读列表</param>
    public SelectPackageDialog(string title, string description, IReadOnlyList<string> candidates)
    {
        // 设置对话框基本属性
        Text = title;
        Width = 680;
        Height = 420;
        StartPosition = FormStartPosition.CenterParent;

        // 创建主布局面板，采用表格布局，3行1列
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12),
        };
        // 定义行样式：第1行自适应高度，第2行填充剩余空间，第3行自适应高度
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        // 添加描述标签到第1行
        root.Controls.Add(new Label { Text = description, AutoSize = true }, 0, 0);

        // 初始化列表框并填充候选数据
        _list = new ListBox { Dock = DockStyle.Fill };
        _list.Items.AddRange(candidates.Cast<object>().ToArray());
        // 双击列表项时触发确认操作
        _list.DoubleClick += (_, _) => Confirm();
        root.Controls.Add(_list, 0, 1);

        // 创建底部按钮面板，使用流式布局，方向为从右到左
        var actions = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
        };
        // 创建确定按钮
        var ok = new Button { Text = "确定", AutoSize = true };
        ok.Click += (_, _) => Confirm();
        // 创建取消按钮
        var cancel = new Button { Text = "取消", AutoSize = true };
        cancel.Click += (_, _) =>
        {
            // 设置对话框结果为取消并关闭
            DialogResult = DialogResult.Cancel;
            Close();
        };
        // 按从右到左顺序添加按钮（确定在左，取消在右）
        actions.Controls.Add(ok);
        actions.Controls.Add(cancel);
        root.Controls.Add(actions, 0, 2);
    }

    /// <summary>
    /// 执行确认选择操作。
    /// </summary>
    private void Confirm()
    {
        // 检查是否选中了有效路径
        if (_list.SelectedItem is not string path)
        {
            // 未选择时显示提示信息
            MessageBox.Show("请先选择一个目标。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // 设置选中路径并关闭对话框
        SelectedPath = path;
        DialogResult = DialogResult.OK;
        Close();
    }
}
