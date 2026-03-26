using System.ComponentModel;
using OvercookedTool.Core.Models;

namespace OvercookedTool.App;

internal sealed class SaveTimelineForm : Form
{
    private readonly IReadOnlyList<SaveBackupEntry> _history;
    private readonly FlowLayoutPanel _timelineFlow;
    private readonly Label _detailLabel;
    private readonly Button _restoreButton;
    private Panel? _selectedPanel;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SaveBackupEntry? SelectedBackup { get; private set; }

    public SaveTimelineForm(string saveFileName, IReadOnlyList<SaveBackupEntry> history)
    {
        _history = history;

        Text = $"历史版本 - {saveFileName}";
        Width = 860;
        Height = 420;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 360);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var header = new Label
        {
            AutoSize = true,
            Text = $"存档: {saveFileName}    历史版本: {history.Count}",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 2, 0, 8),
        };
        root.Controls.Add(header, 0, 0);

        _timelineFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8, 12, 8, 8),
        };
        root.Controls.Add(_timelineFlow, 0, 1);

        _detailLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(820, 0),
            Text = "请选择一个历史版本。",
            Margin = new Padding(0, 2, 0, 6),
        };
        root.Controls.Add(_detailLabel, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
        };
        var closeButton = new Button { Text = "关闭", AutoSize = true };
        closeButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        _restoreButton = new Button { Text = "恢复到此版本", AutoSize = true, Enabled = false };
        _restoreButton.Click += (_, _) =>
        {
            if (SelectedBackup is null)
            {
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };
        var openButton = new Button { Text = "打开备份目录", AutoSize = true };
        openButton.Click += (_, _) =>
        {
            var path = SelectedBackup?.BackupPath ?? _history.FirstOrDefault()?.BackupPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                System.Diagnostics.Process.Start("explorer.exe", dir);
            }
        };
        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(_restoreButton);
        buttons.Controls.Add(openButton);
        root.Controls.Add(buttons, 0, 3);

        BuildCards();
    }

    private void BuildCards()
    {
        _timelineFlow.Controls.Clear();
        if (_history.Count == 0)
        {
            _timelineFlow.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "暂无历史版本。执行覆盖、删除、编辑操作后会自动产生备份。",
                ForeColor = Color.FromArgb(97, 97, 97),
                Margin = new Padding(6, 8, 0, 0),
            });
            return;
        }

        for (var i = 0; i < _history.Count; i++)
        {
            var info = _history[i];
            var card = BuildCard(i, info);
            _timelineFlow.Controls.Add(card);
        }
    }

    private Panel BuildCard(int index, SaveBackupEntry backup)
    {
        var card = new Panel
        {
            Width = 92,
            Height = 108,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 18, 0),
            Cursor = Cursors.Hand,
            Tag = backup,
        };

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 26,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = $"V{_history.Count - index}",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
        };
        card.Controls.Add(title);

        var date = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = backup.CreatedAt.ToString("MM-dd"),
        };
        card.Controls.Add(date);

        var time = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = backup.CreatedAt.ToString("HH:mm:ss"),
        };
        card.Controls.Add(time);

        var size = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopCenter,
            Text = $"{Math.Max(1, backup.Size / 1024)} KB\n{backup.Reason}",
            ForeColor = Color.FromArgb(97, 97, 97),
        };
        card.Controls.Add(size);

        card.Click += (_, _) => SelectCard(card, backup);
        foreach (Control child in card.Controls)
        {
            child.Click += (_, _) => SelectCard(card, backup);
        }

        if (index == 0)
        {
            SelectCard(card, backup);
        }

        return card;
    }

    private void SelectCard(Panel card, SaveBackupEntry backup)
    {
        if (_selectedPanel is not null)
        {
            _selectedPanel.BackColor = Color.White;
        }

        _selectedPanel = card;
        _selectedPanel.BackColor = Color.FromArgb(227, 242, 253);
        SelectedBackup = backup;
        _restoreButton.Enabled = true;
        _detailLabel.Text = $"备份文件: {backup.BackupPath}\n动作: {backup.Reason}  时间: {backup.CreatedAt:yyyy-MM-dd HH:mm:ss}";
    }
}
