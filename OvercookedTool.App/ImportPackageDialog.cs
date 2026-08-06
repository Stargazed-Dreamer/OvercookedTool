using System.ComponentModel;
using OvercookedTool.Core.Services;

namespace OvercookedTool.App;

/// <summary>
/// 导入存档包对话框，用于选择或输入存档目录路径，支持浏览文件夹、查看历史记录和自动检测候选路径。
/// </summary>
internal sealed class ImportPackageDialog : Form
{
    private readonly SavePackageService _service;
    private readonly AppSettings _settings;
    private readonly TextBox _pathInput;
    private readonly ListBox _historyList;
    private readonly ListBox _autoDetectList;

    /// <summary>
    /// 用户选择的存档目录路径，如果对话框未确认则为null。
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? SelectedPath { get; private set; }

    /// <summary>
    /// 初始化导入存档包对话框。
    /// </summary>
    /// <param name="service">存档包服务，用于查找候选存档包。</param>
    /// <param name="settings">应用程序设置，包含历史记录和自动检测配置。</param>
    public ImportPackageDialog(SavePackageService service, AppSettings settings)
    {
        _service = service;
        _settings = settings;

        // 设置窗口基本属性
        Text = "导入存档包";
        Width = 760;
        Height = 540;
        StartPosition = FormStartPosition.CenterParent;

        // 创建根布局容器，使用表格布局管理控件排列
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12),
        };
        // 设置行样式：第一行和第四行自动调整大小，中间两行平分剩余空间
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        // 创建路径输入行布局，包含文本框、浏览按钮和导入按钮
        var pathRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
        };
        // 设置列样式：第一列填满剩余空间，后两列自动调整大小
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _pathInput = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "输入或选择存档目录..." };
        pathRow.Controls.Add(_pathInput, 0, 0);

        // 创建浏览按钮，点击时触发文件夹选择对话框
        var browse = new Button { Text = "浏览...", AutoSize = true };
        browse.Click += (_, _) => BrowseFolder();
        pathRow.Controls.Add(browse, 1, 0);

        // 创建导入按钮，设置样式并绑定确认打开事件
        var open = new Button
        {
            Text = "导入",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(33, 150, 243),
            ForeColor = Color.White,
        };
        open.FlatAppearance.BorderSize = 0;
        open.Click += (_, _) => ConfirmOpen();
        pathRow.Controls.Add(open, 2, 0);
        root.Controls.Add(pathRow, 0, 0);

        // 创建历史记录分组框，包含列表框显示最近使用的路径
        var historyGroup = new GroupBox
        {
            Text = "历史记录",
            Dock = DockStyle.Fill,
        };
        _historyList = new ListBox { Dock = DockStyle.Fill };
        // 双击历史记录项时直接导入该路径
        _historyList.DoubleClick += (_, _) => UseSelectedAndConfirm(_historyList);
        // 选择历史记录项时更新路径输入框
        _historyList.SelectedIndexChanged += (_, _) =>
        {
            if (_historyList.SelectedItem is string path)
            {
                _pathInput.Text = path;
            }
        };
        historyGroup.Controls.Add(_historyList);
        root.Controls.Add(historyGroup, 0, 1);

        // 创建自动检测分组框，包含列表框显示程序自动找到的候选路径
        var autoGroup = new GroupBox
        {
            Text = "自动检测候选路径",
            Dock = DockStyle.Fill,
        };
        _autoDetectList = new ListBox { Dock = DockStyle.Fill };
        // 双击候选路径项时直接导入该路径
        _autoDetectList.DoubleClick += (_, _) => UseSelectedAndConfirm(_autoDetectList);
        // 选择候选路径项时更新路径输入框
        _autoDetectList.SelectedIndexChanged += (_, _) =>
        {
            if (_autoDetectList.SelectedItem is string path)
            {
                _pathInput.Text = path;
            }
        };
        autoGroup.Controls.Add(_autoDetectList);
        root.Controls.Add(autoGroup, 0, 2);

        // 创建提示标签，说明功能用法
        var tips = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Text = "支持直接选择包含子目录的上级目录，程序会自动检测并提示候选存档包。",
            ForeColor = Color.FromArgb(97, 97, 97),
        };
        root.Controls.Add(tips, 0, 3);

        // 加载历史记录和自动检测路径数据
        LoadHistory();
        LoadAutoDetectPaths();
    }

    /// <summary>
    /// 打开文件夹浏览器对话框，让用户选择存档目录。
    /// </summary>
    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择存档目录或其上级目录",
            UseDescriptionForTitle = true,
        };
        // 如果用户点击了确定，则将选择的路径显示在输入框中
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _pathInput.Text = dialog.SelectedPath;
        }
    }

    /// <summary>
    /// 从设置中加载最近使用的存档路径，并显示在历史记录列表中（仅显示仍然存在的目录）。
    /// </summary>
    private void LoadHistory()
    {
        _historyList.Items.Clear();
        foreach (var path in _settings.RecentPackagePaths)
        {
            // 只添加实际存在的目录路径
            if (Directory.Exists(path))
            {
                _historyList.Items.Add(path);
            }
        }
    }

    /// <summary>
    /// 加载程序自动检测到的候选存档路径，显示在自动检测列表中。
    /// 如果设置中禁用了自动检测，则显示提示信息。
    /// </summary>
    private void LoadAutoDetectPaths()
    {
        _autoDetectList.Items.Clear();
        _autoDetectList.Enabled = true;
        // 检查是否在设置中启用了自动检测功能
        if (!_settings.EnableAutoDetectOnImport)
        {
            _autoDetectList.Items.Add("已在设置中关闭自动检测。");
            _autoDetectList.Enabled = false;
            return;
        }

        // 获取默认检测根目录，并收集所有找到的候选路径（去重，不区分大小写）
        var roots = GetDefaultDetectionRoots();
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            // 在每个根目录下搜索候选存档包，限制最大深度和数量以避免过度搜索
            foreach (var candidate in _service.FindCandidatePackages(root, maxDepth: 4, maxCount: 80))
            {
                // 如果路径是新的（未重复），则添加到列表
                if (found.Add(candidate))
                {
                    _autoDetectList.Items.Add(candidate);
                }
            }
        }

        // 如果没有找到任何候选路径，则显示提示信息并禁用列表
        if (_autoDetectList.Items.Count == 0)
        {
            _autoDetectList.Items.Add("未发现候选路径，可手动浏览。");
            _autoDetectList.Enabled = false;
        }
    }

    /// <summary>
    /// 获取默认的自动检测根目录列表，包括游戏常见安装路径、Steam库和文档目录。
    /// </summary>
    /// <returns>去重后的目录路径列表（不区分大小写）。</returns>
    private static IReadOnlyList<string> GetDefaultDetectionRoots()
    {
        var list = new List<string>();
        // 获取本地应用数据目录，并计算可能的游戏安装路径
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            // 计算LocalLow目录下的Team17文件夹，这是Overcooked游戏的常见存档位置
            var team17Root = Path.GetFullPath(Path.Combine(localAppData, "..", "LocalLow", "Team17"));
            var gameRoots = new[]
            {
                Path.Combine(team17Root, "Overcooked2"),
                Path.Combine(team17Root, "Overcooked"),
                Path.Combine(team17Root, "Overcooked All You Can Eat"),
            };
            // 将存在的游戏目录添加到列表
            foreach (var root in gameRoots)
            {
                if (Directory.Exists(root))
                {
                    list.Add(root);
                }
            }
        }

        // 在所有固定磁盘上查找Steam库目录
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
        {
            var maybeSteam = Path.Combine(drive.RootDirectory.FullName, "SteamLibrary", "steamapps");
            if (Directory.Exists(maybeSteam))
            {
                list.Add(maybeSteam);
            }
        }

        // 添加文档目录（通常用于存储用户数据）
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (Directory.Exists(docs))
        {
            list.Add(docs);
        }

        // 返回去重后的列表，忽略大小写差异
        return list
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 确认并打开用户输入的目录路径。如果路径无效，则显示提示信息。
    /// </summary>
    private void ConfirmOpen()
    {
        var input = _pathInput.Text.Trim();
        // 验证路径是否为空或不存在
        if (string.IsNullOrWhiteSpace(input) || !Directory.Exists(input))
        {
            MessageBox.Show("请输入有效目录路径。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // 设置选定的路径并关闭对话框，返回OK结果
        SelectedPath = input;
        DialogResult = DialogResult.OK;
        Close();
    }

    /// <summary>
    /// 从指定列表框中获取选中的路径，并尝试确认打开。
    /// </summary>
    /// <param name="list">历史记录或自动检测列表框。</param>
    private void UseSelectedAndConfirm(ListBox list)
    {
        // 如果列表中没有选中项，则直接返回
        if (list.SelectedItem is not string path)
        {
            return;
        }

        // 将选中的路径填入输入框，并尝试确认打开
        _pathInput.Text = path;
        ConfirmOpen();
    }
}
