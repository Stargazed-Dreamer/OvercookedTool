using System.ComponentModel;
using OvercookedTool.Core.Models;

namespace OvercookedTool.App;

/// <summary>
/// 用于展示和操作存档文件历史版本的时间线窗体。
/// </summary>
internal sealed class SaveTimelineForm : Form
{
    private readonly IReadOnlyList<SaveBackupEntry> _history;
    private readonly FlowLayoutPanel _timelineFlow;
    private readonly Label _detailLabel;
    private readonly Button _restoreButton;
    private Panel? _selectedPanel;

    /// <summary>
    /// 获取用户选中的备份条目。该属性不可见于设计器，且不参与序列化。
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SaveBackupEntry? SelectedBackup { get; private set; }

    /// <summary>
    /// 初始化历史版本时间线窗体。
    /// </summary>
    /// <param name="saveFileName">存档文件名，用于窗体标题显示。</param>
    /// <param name="history">存档的历史版本列表。</param>
    public SaveTimelineForm(string saveFileName, IReadOnlyList<SaveBackupEntry> history)
    {
        _history = history;

        Text = $"历史版本 - {saveFileName}";
        Width = 860;
        Height = 420;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 360);

        // 创建根布局容器
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

        // 创建显示存档名称和版本总数的头部标签
        var header = new Label
        {
            AutoSize = true,
            Text = $"存档: {saveFileName}    历史版本: {history.Count}",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 2, 0, 8),
        };
        root.Controls.Add(header, 0, 0);

        // 创建可水平滚动的流布局面板，用于存放历史版本卡片
        _timelineFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8, 12, 8, 8),
        };
        root.Controls.Add(_timelineFlow, 0, 1);

        // 创建用于显示当前选中版本详情的标签
        _detailLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(820, 0),
            Text = "请选择一个历史版本。",
            Margin = new Padding(0, 2, 0, 6),
        };
        root.Controls.Add(_detailLabel, 0, 2);

        // 创建按钮面板（使用右对齐的流布局）
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
            // 如果没有选中任何备份，则不执行操作
            if (SelectedBackup is null)
            {
                return;
            }

            // 设置对话框结果为OK，表示用户确认恢复，并关闭窗体
            DialogResult = DialogResult.OK;
            Close();
        };
        var openButton = new Button { Text = "打开备份目录", AutoSize = true };
        openButton.Click += (_, _) =>
        {
            // 优先使用当前选中备份的路径，若无则尝试使用列表中第一个备份的路径
            var path = SelectedBackup?.BackupPath ?? _history.FirstOrDefault()?.BackupPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            // 获取备份文件所在的目录路径并尝试用资源管理器打开
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

        // 调用方法构建历史版本的时间线卡片
        BuildCards();
    }

    /// <summary>
    /// 构建或刷新时间线上的历史版本卡片。
    /// </summary>
    private void BuildCards()
    {
        // 清空流布局中的所有旧控件
        _timelineFlow.Controls.Clear();
        // 如果历史记录为空，则显示提示信息
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

        // 遍历历史版本列表，为每个版本创建卡片并添加到时间线
        for (var i = 0; i < _history.Count; i++)
        {
            var info = _history[i];
            var card = BuildCard(i, info);
            _timelineFlow.Controls.Add(card);
        }
    }

    /// <summary>
    /// 根据历史版本信息和索引，构建一个可视化的版本卡片。
    /// </summary>
    /// <param name="index">当前版本在列表中的索引。</param>
    /// <param name="backup">历史版本的数据对象。</param>
    /// <returns>构建好的卡片面板控件。</returns>
    private Panel BuildCard(int index, SaveBackupEntry backup)
    {
        // 创建卡片容器面板
        var card = new Panel
        {
            Width = 92,
            Height = 108,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 18, 0),
            Cursor = Cursors.Hand,
            // 使用Tag属性存储关联的备份数据
            Tag = backup,
        };

        // 版本标题标签（显示V序号）
        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 26,
            TextAlign = ContentAlignment.MiddleCenter,
            // 版本序号显示为倒序，最新的为V1
            Text = $"V{_history.Count - index}",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
        };
        card.Controls.Add(title);

        // 日期标签
        var date = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = backup.CreatedAt.ToString("MM-dd"),
        };
        card.Controls.Add(date);

        // 时间标签
        var time = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = backup.CreatedAt.ToString("HH:mm:ss"),
        };
        card.Controls.Add(time);

        // 文件大小和备份原因标签（填充剩余空间）
        var size = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopCenter,
            Text = $"{Math.Max(1, backup.Size / 1024)} KB\n{backup.Reason}",
            ForeColor = Color.FromArgb(97, 97, 97),
        };
        card.Controls.Add(size);

        // 为卡片及其所有子控件绑定点击事件，确保点击任何部分都能选中卡片
        card.Click += (_, _) => SelectCard(card, backup);
        foreach (Control child in card.Controls)
        {
            child.Click += (_, _) => SelectCard(card, backup);
        }

        // 默认选中第一个（最新的）卡片
        if (index == 0)
        {
            SelectCard(card, backup);
        }

        return card;
    }

    /// <summary>
    /// 处理卡片选中事件，更新界面状态以反映当前选择。
    /// </summary>
    /// <param name="card">被点击的卡片面板。</param>
    /// <param name="backup">该卡片对应的备份数据。</param>
    private void SelectCard(Panel card, SaveBackupEntry backup)
    {
        // 将之前选中的卡片恢复为默认白色背景
        if (_selectedPanel is not null)
        {
            _selectedPanel.BackColor = Color.White;
        }

        // 设置当前选中的卡片，并更改其背景色以高亮显示
        _selectedPanel = card;
        _selectedPanel.BackColor = Color.FromArgb(227, 242, 253);
        // 更新选中的备份数据
        SelectedBackup = backup;
        // 启用“恢复”按钮
        _restoreButton.Enabled = true;
        // 更新详情标签，显示当前选中备份的完整信息
        _detailLabel.Text = $"备份文件: {backup.BackupPath}\n动作: {backup.Reason}  时间: {backup.CreatedAt:yyyy-MM-dd HH:mm:ss}";
    }
}
