using OvercookedTool.Core.Models;

namespace OvercookedTool.App;

internal sealed class SaveTileControl : UserControl
{
    private readonly Label _starLabel;
    private readonly Label _slotLabel;
    private readonly Button _leftButton;
    private readonly Button _rightButton;
    private bool _selected;
    private bool _pending;

    public SaveFileEntry Entry { get; }

    public event EventHandler<SaveFileEntry>? TileClicked;
    public event EventHandler<SaveFileEntry>? TileDoubleClicked;
    public event EventHandler<MovePositionRequest>? MoveRequested;

    public SaveTileControl(SaveFileEntry entry)
    {
        Entry = entry;
        Width = 128;
        Height = 128;
        Margin = new Padding(6);
        BackColor = Color.FromArgb(248, 250, 252);
        UiPerformance.EnableDoubleBuffer(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10, 8, 10, 8),
            Margin = new Padding(0),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        Controls.Add(root);

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

        WireClick(this);
        ApplyVisualStyle();
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        ApplyVisualStyle();
    }

    public void SetPending(bool pending)
    {
        _pending = pending;
        ApplyVisualStyle();
    }

    public void SetMultiSelect(bool enabled)
    {
        var visible = !enabled && !Entry.IsMeta;
        _leftButton.Visible = visible;
        _rightButton.Visible = visible;
    }

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
        button.Click += (_, _) => MoveRequested?.Invoke(this, new MovePositionRequest(Entry, direction));
        return button;
    }

    private void WireClick(Control control)
    {
        control.Click += (_, _) => TileClicked?.Invoke(this, Entry);
        control.DoubleClick += (_, _) => TileDoubleClicked?.Invoke(this, Entry);
        foreach (Control child in control.Controls)
        {
            WireClick(child);
        }
    }

    private void ApplyVisualStyle()
    {
        if (_pending)
        {
            BackColor = _selected ? Color.FromArgb(255, 244, 179) : Color.FromArgb(255, 249, 196);
        }
        else
        {
            BackColor = _selected ? Color.FromArgb(227, 242, 253) : Color.FromArgb(248, 250, 252);
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var border = _pending
            ? Color.FromArgb(255, 193, 7)
            : (_selected ? Color.FromArgb(33, 150, 243) : Color.FromArgb(220, 224, 230));
        using var pen = new Pen(border, _selected ? 2 : 1);
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        e.Graphics.DrawRectangle(pen, rect);
    }
}
