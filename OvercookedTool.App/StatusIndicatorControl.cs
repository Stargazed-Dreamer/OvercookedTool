using System.ComponentModel;

namespace OvercookedTool.App;

internal sealed class StatusIndicatorControl : Control
{
    private bool? _status;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool? Status
    {
        get => _status;
        set
        {
            _status = value;
            Invalidate();
        }
    }

    public StatusIndicatorControl()
    {
        Size = new Size(20, 20);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        if (_status is null)
        {
            return;
        }

        var color = _status.Value ? Color.FromArgb(76, 175, 80) : Color.FromArgb(244, 67, 54);
        using var pen = new Pen(color, 2);
        if (_status.Value)
        {
            e.Graphics.DrawLines(
                pen,
                new[]
                {
                    new Point((int)(Width * 0.2), (int)(Height * 0.55)),
                    new Point((int)(Width * 0.42), (int)(Height * 0.78)),
                    new Point((int)(Width * 0.82), (int)(Height * 0.25)),
                });
        }
        else
        {
            e.Graphics.DrawLine(pen, (int)(Width * 0.2), (int)(Height * 0.2), (int)(Width * 0.8), (int)(Height * 0.8));
            e.Graphics.DrawLine(pen, (int)(Width * 0.8), (int)(Height * 0.2), (int)(Width * 0.2), (int)(Height * 0.8));
        }
    }
}
