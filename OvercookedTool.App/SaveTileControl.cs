using OvercookedTool.Core.Models;

namespace OvercookedTool.App;

/// <summary>
/// 保存游戏存档的图块控件，用于显示存档信息并提供交互操作
/// </summary>
internal sealed class SaveTileControl : UserControl
{
    private readonly Label _starLabel;
    private readonly Label _slotLabel;
    private readonly Button _leftButton;
    private readonly Button _rightButton;
    private bool _selected;
    private bool _pending;

    /// <summary>
    /// 当前控件关联的存档条目
    /// </summary>
    public SaveFileEntry Entry { get; }

    /// <summary>
    /// 点击图块时触发的事件，传递对应的存档条目
    /// </summary>
    public event EventHandler<SaveFileEntry>? TileClicked;
    /// <summary>
    /// 双击图块时触发的事件，传递对应的存档条目
    /// </summary>
    public event EventHandler<SaveFileEntry>? TileDoubleClicked;
    /// <summary>
    /// 请求移动图块位置时触发的事件，传递移动位置请求
    /// </summary>
    public event EventHandler<MovePositionRequest>? MoveRequested;

    /// <summary>
    /// 初始化存档图块控件
    /// </summary>
    /// <param name="entry">要显示的存档条目</param>
    public SaveTileControl(SaveFileEntry entry)
    {
        Entry = entry;
        Width = 128;
        Height = 128;
        Margin = new Padding(6);
        BackColor = Color.FromArgb(248, 250, 252);
        UiPerformance.EnableDoubleBuffer(this);  // 启用双缓冲减少绘制闪烁

        // 创建根容器布局
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10, 8, 10, 8),
            Margin = new Padding(0),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));  // 箭头按钮行
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // 星星数量显示行
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));   // 存档槽号显示行
        Controls.Add(root);

        // 创建左右箭头按钮容器
        var arrows = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
            AutoSize = false,
        };
        arrows.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        arrows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        arrows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        _leftButton = BuildArrowButton("◀", "left");
        _rightButton = BuildArrowButton("▶", "right");
        arrows.Controls.Add(_leftButton, 0, 0);
        arrows.Controls.Add(_rightButton, 1, 0);
        root.Controls.Add(arrows, 0, 0);

        // 显示星星数量的标签，如果没有数据则显示"--"
        _starLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            Text = entry.StarCount.HasValue ? $"{entry.StarCount.Value}⭐" : "--⭐",
            Margin = new Padding(0),
            Padding = new Padding(0, 0, 4, 0),
            UseCompatibleTextRendering = true,
        };
        root.Controls.Add(_starLabel, 0, 1);

        // 显示存档槽号的标签，Meta存档显示"Meta"，否则显示档位序号
        _slotLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            Text = entry.IsMeta ? "Meta" : $"档位 {entry.Slot + 1}",
            Margin = new Padding(0),
            Padding = new Padding(0, 0, 4, 0),
        };
        root.Controls.Add(_slotLabel, 0, 2);

        // 递归绑定所有子控件的点击事件
        WireClick(this);
        // 应用初始视觉样式
        ApplyVisualStyle();
    }

    /// <summary>
    /// 设置控件的选中状态
    /// </summary>
    /// <param name="selected">是否选中</param>
    public void SetSelected(bool selected)
    {
        _selected = selected;
        ApplyVisualStyle();  // 状态改变时更新视觉样式
    }

    /// <summary>
    /// 设置控件的挂起状态（通常表示正在保存或加载）
    /// </summary>
    /// <param name="pending">是否处于挂起状态</param>
    public void SetPending(bool pending)
    {
        _pending = pending;
        ApplyVisualStyle();  // 状态改变时更新视觉样式
    }

    /// <summary>
    /// 设置是否启用多选模式
    /// </summary>
    /// <param name="enabled">是否启用多选模式</param>
    public void SetMultiSelect(bool enabled)
    {
        // 多选模式下隐藏箭头按钮，且Meta存档不显示箭头按钮
        var visible = !enabled && !Entry.IsMeta;
        _leftButton.Visible = visible;
        _rightButton.Visible = visible;
    }

    /// <summary>
    /// 创建方向箭头按钮
    /// </summary>
    /// <param name="buttonText">按钮显示文本</param>
    /// <param name="direction">移动方向标识</param>
    /// <returns>配置好的箭头按钮控件</returns>
    private Button BuildArrowButton(string text, string direction)
    {
        var button = new Button
        {
            Text = text,
            Width = 30,
            Height = 29,
            Anchor = AnchorStyles.None,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(227, 242, 253),
            Margin = new Padding(3, 0, 3, 0),
            Padding = new Padding(0),
        };
        button.FlatAppearance.BorderSize = 0;
        // 绑定点击事件，触发移动请求
        button.Click += (_, _) => MoveRequested?.Invoke(this, new MovePositionRequest(Entry, direction));
        return button;
    }

    /// <summary>
    /// 递归绑定控件的点击和双击事件
    /// </summary>
    /// <param name="control">要绑定事件的控件</param>
    private void WireClick(Control control)
    {
        // 绑定单击和双击事件
        control.Click += (_, _) => TileClicked?.Invoke(this, Entry);
        control.DoubleClick += (_, _) => TileDoubleClicked?.Invoke(this, Entry);
        // 递归绑定所有子控件
        foreach (Control child in control.Controls)
        {
            WireClick(child);
        }
    }

    /// <summary>
    /// 根据控件状态应用对应的视觉样式
    /// </summary>
    private void ApplyVisualStyle()
    {
        // 根据挂起状态和选中状态设置不同的背景颜色
        if (_pending)
        {
            // 挂起状态：选中为较深黄色，未选中为较浅黄色
            BackColor = _selected ? Color.FromArgb(255, 244, 179) : Color.FromArgb(255, 249, 196);
        }
        else
        {
            // 正常状态：选中为较深蓝色，未选中为默认灰色
            BackColor = _selected ? Color.FromArgb(227, 242, 253) : Color.FromArgb(248, 250, 252);
        }

        Invalidate();  // 触发重绘
    }

    /// <summary>
    /// 重写绘制方法，绘制边框
    /// </summary>
    /// <param name="e">绘制事件参数</param>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // 根据状态选择边框颜色
        var border = _pending
            ? Color.FromArgb(255, 193, 7)   // 挂起状态：黄色边框
            : (_selected ? Color.FromArgb(33, 150, 243) : Color.FromArgb(220, 224, 230));
        // 根据选中状态选择边框宽度
        using var pen = new Pen(border, _selected ? 2 : 1);
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        e.Graphics.DrawRectangle(pen, rect);  // 绘制边框矩形
    }
}
