using OvercookedTool.Core.Services;
using System.ComponentModel;

namespace OvercookedTool.App;

internal sealed class ImportPackageDialog : Form
{
    private readonly SavePackageService _service;
    private readonly AppSettings _settings;
    private readonly TextBox _pathInput;
    private readonly ListBox _historyList;
    private readonly ListBox _autoDetectList;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? SelectedPath { get; private set; }

    public ImportPackageDialog(SavePackageService service, AppSettings settings)
    {
        _service = service;
        _settings = settings;

        Text = "导入存档包";
        Width = 760;
        Height = 540;
        StartPosition = FormStartPosition.CenterParent;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var pathRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
        };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _pathInput = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "输入或选择存档目录..." };
        pathRow.Controls.Add(_pathInput, 0, 0);

        var browse = new Button { Text = "浏览...", AutoSize = true };
        browse.Click += (_, _) => BrowseFolder();
        pathRow.Controls.Add(browse, 1, 0);

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

        var historyGroup = new GroupBox
        {
            Text = "历史记录",
            Dock = DockStyle.Fill,
        };
        _historyList = new ListBox { Dock = DockStyle.Fill };
        _historyList.DoubleClick += (_, _) => UseSelectedAndConfirm(_historyList);
        _historyList.SelectedIndexChanged += (_, _) =>
        {
            if (_historyList.SelectedItem is string path)
            {
                _pathInput.Text = path;
            }
        };
        historyGroup.Controls.Add(_historyList);
        root.Controls.Add(historyGroup, 0, 1);

        var autoGroup = new GroupBox
        {
            Text = "自动检测候选路径",
            Dock = DockStyle.Fill,
        };
        _autoDetectList = new ListBox { Dock = DockStyle.Fill };
        _autoDetectList.DoubleClick += (_, _) => UseSelectedAndConfirm(_autoDetectList);
        _autoDetectList.SelectedIndexChanged += (_, _) =>
        {
            if (_autoDetectList.SelectedItem is string path)
            {
                _pathInput.Text = path;
            }
        };
        autoGroup.Controls.Add(_autoDetectList);
        root.Controls.Add(autoGroup, 0, 2);

        var tips = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Text = "支持直接选择包含子目录的上级目录，程序会自动检测并提示候选存档包。",
            ForeColor = Color.FromArgb(97, 97, 97),
        };
        root.Controls.Add(tips, 0, 3);

        LoadHistory();
        LoadAutoDetectPaths();
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择存档目录或其上级目录",
            UseDescriptionForTitle = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _pathInput.Text = dialog.SelectedPath;
        }
    }

    private void LoadHistory()
    {
        _historyList.Items.Clear();
        foreach (var path in _settings.RecentPackagePaths)
        {
            if (Directory.Exists(path))
            {
                _historyList.Items.Add(path);
            }
        }
    }

    private void LoadAutoDetectPaths()
    {
        _autoDetectList.Items.Clear();
        _autoDetectList.Enabled = true;
        if (!_settings.EnableAutoDetectOnImport)
        {
            _autoDetectList.Items.Add("已在设置中关闭自动检测。");
            _autoDetectList.Enabled = false;
            return;
        }

        var roots = GetDefaultDetectionRoots();
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            foreach (var candidate in _service.FindCandidatePackages(root, maxDepth: 4, maxCount: 80))
            {
                if (found.Add(candidate))
                {
                    _autoDetectList.Items.Add(candidate);
                }
            }
        }

        if (_autoDetectList.Items.Count == 0)
        {
            _autoDetectList.Items.Add("未发现候选路径，可手动浏览。");
            _autoDetectList.Enabled = false;
        }
    }

    private static IReadOnlyList<string> GetDefaultDetectionRoots()
    {
        var list = new List<string>();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            var team17Root = Path.GetFullPath(Path.Combine(localAppData, "..", "LocalLow", "Team17"));
            var gameRoots = new[]
            {
                Path.Combine(team17Root, "Overcooked2"),
                Path.Combine(team17Root, "Overcooked"),
                Path.Combine(team17Root, "Overcooked All You Can Eat"),
            };
            foreach (var root in gameRoots)
            {
                if (Directory.Exists(root))
                {
                    list.Add(root);
                }
            }
        }

        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
        {
            var maybeSteam = Path.Combine(drive.RootDirectory.FullName, "SteamLibrary", "steamapps");
            if (Directory.Exists(maybeSteam))
            {
                list.Add(maybeSteam);
            }
        }

        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (Directory.Exists(docs))
        {
            list.Add(docs);
        }

        return list
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ConfirmOpen()
    {
        var input = _pathInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(input) || !Directory.Exists(input))
        {
            MessageBox.Show("请输入有效目录路径。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SelectedPath = input;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void UseSelectedAndConfirm(ListBox list)
    {
        if (list.SelectedItem is not string path)
        {
            return;
        }

        _pathInput.Text = path;
        ConfirmOpen();
    }
}
