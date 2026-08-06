using System.ComponentModel;

namespace OvercookedTool.App;

/// <summary>
/// 状态指示器控件，用于在界面上显示状态指示，例如勾号（表示成功）或叉号（表示失败）。
/// </summary>
internal sealed class StatusIndicatorControl : Control
{
    // 私有字段，存储控件的状态值，可为 null（无状态）、true（成功）或 false（失败）。
    private bool? _status;

    /// <summary>
    /// 获取或设置控件的状态。设置时会触发重绘以更新显示。
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool? Status
    {
        get => _status;
        set
        {
            _status = value;
            // 状态改变时使控件无效化，触发重绘。
            Invalidate();
        }
    }

    /// <summary>
    /// 初始化 StatusIndicatorControl 的新实例，设置控件大小并启用双缓冲和自定义绘制样式。
    /// </summary>
    public StatusIndicatorControl()
    {
        // 设置控件的默认大小为 20x20 像素。
        Size = new Size(20, 20);
        // 启用所有绘制在 WM_PAINT 消息中处理、优化双缓冲和用户自定义绘制，以减少闪烁。
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
    }

    /// <summary>
    /// 处理控件的绘制事件，根据状态值绘制勾号或叉号图标。
    /// </summary>
    /// <param name="e">包含绘制事件数据的参数。</param>
    protected override void OnPaint(PaintEventArgs e)
    {
        // 调用基类的 OnPaint 方法以确保标准绘制行为。
        base.OnPaint(e);
        // 设置图形为抗锯齿模式，使绘制更平滑。
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        // 如果状态为 null，则不绘制任何内容并直接返回。
        if (_status is null)
        {
            return;
        }

        // 根据状态值选择颜色：true 使用绿色 (RGB 76,175,80)，false 使用红色 (RGB 244,67,54)。
        var color = _status.Value ? Color.FromArgb(76, 175, 80) : Color.FromArgb(244, 67, 54);
        // 创建指定颜色和宽度的画笔，using 确保资源被正确释放。
        using var pen = new Pen(color, 2);
        if (_status.Value)
        {
            // 当状态为 true 时，绘制勾号（三条线段组成）。
            e.Graphics.DrawLines(
                pen,
                new[]
                {
                    // 勾号的起始点、转折点和结束点，基于控件宽度和高度的相对位置。
                    new Point((int)(Width * 0.2), (int)(Height * 0.55)),
                    new Point((int)(Width * 0.42), (int)(Height * 0.78)),
                    new Point((int)(Width * 0.82), (int)(Height * 0.25)),
                });
        }
        else
        {
            // 当状态为 false 时，绘制叉号（两条交叉的对角线）。
            // 第一条对角线：从左上到右下。
            e.Graphics.DrawLine(pen, (int)(Width * 0.2), (int)(Height * 0.2), (int)(Width * 0.8), (int)(Height * 0.8));
            // 第二条对角线：从右上到左下。
            e.Graphics.DrawLine(pen, (int)(Width * 0.8), (int)(Height * 0.2), (int)(Width * 0.2), (int)(Height * 0.8));
        }
    }
}
