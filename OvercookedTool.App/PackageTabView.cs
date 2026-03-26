using System.ComponentModel;
using System.Diagnostics;
using OvercookedTool.Core.Models;

namespace OvercookedTool.App;

internal sealed class PackageTabView : UserControl
{
    private TextBox _pathInput = null!;
    private Label _friendCodeValue = null!;
    private Label _versionValue = null!;
    private CheckBox _multiSelectCheckBox = null!;
    private CheckBox _translateCheck = null!;
    private SaveTileMatrixView _tileMatrix = null!;

    private TextBox _keyInput = null!;
    private StatusIndicatorControl _keyStatus = null!;
    private ListBox _conflictList = null!;
    private StatusIndicatorControl _conflictStatus = null!;
    private Button _conflictRefreshButton = null!;
    private Button _conflictResolveButton = null!;
    private ListBox _syncList = null!;
    private StatusIndicatorControl _syncStatus = null!;
    private Button _syncRefreshButton = null!;
    private Button _syncResolveButton = null!;

    private Label _detailLabel = null!;
    private Button _timelineButton = null!;
    private Button _copyButton = null!;
    private Button _moveButton = null!;
    private Button _syncButton = null!;
    private Button _deleteButton = null!;
    private Button _editButton = null!;
    private Button _viewSelectedBackupButton = null!;

    private IReadOnlyList<SaveFileEntry> _selected = Array.Empty<SaveFileEntry>();
    private IReadOnlyList<SaveSyncIssue> _conflicts = Array.Empty<SaveSyncIssue>();
    private IReadOnlyList<SaveSyncIssue> _pendingSync = Array.Empty<SaveSyncIssue>();
    private IReadOnlyList<SaveFileEntry> _pendingDrafts = Array.Empty<SaveFileEntry>();
    private bool _splitInitialized;
    private bool _splitUserResized;

    public event EventHandler<string>? ApplyKeyRequested;
    public event EventHandler? AddSaveRequested;
    public event EventHandler? EditMetaRequested;
    public event EventHandler<TransferRequest>? TransferRequested;
    public event EventHandler<IReadOnlyList<SaveFileEntry>>? SyncToSourceRequested;
    public event EventHandler<IReadOnlyList<SaveFileEntry>>? DeleteRequested;
    public event EventHandler<SaveFileEntry>? EditRequested;
    public event EventHandler<MovePositionRequest>? MovePositionRequested;
    public event EventHandler<SaveFileEntry>? TimelineRequested;
    public event EventHandler? RefreshDiagnosticsRequested;
    public event EventHandler<IReadOnlyList<SaveSyncIssue>>? ResolveConflictsRequested;
    public event EventHandler<IReadOnlyList<SaveSyncIssue>>? ResolvePendingSyncRequested;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SavePackageContext? Context { get; private set; }

    public string ManualKey => _keyInput.Text.Trim();
    public bool TranslateEnabled => _translateCheck.Checked;

    public PackageTabView()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(245, 248, 253);
        UiPerformance.EnableDoubleBuffer(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = BackColor,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var topInfo = BuildTopInfoPanel();
        root.Controls.Add(topInfo, 0, 0);

        var middleSplit = BuildMiddleSplitPanel();
        root.Controls.Add(middleSplit, 0, 1);

        var bottom = BuildBottomPanel();
        root.Controls.Add(bottom, 0, 2);
    }

    public void Bind(SavePackageContext context)
    {
        Context = context;
        _pathInput.Text = context.PackagePath;
        var hasFriendCode = !string.IsNullOrWhiteSpace(context.FriendCode);
        _friendCodeValue.Visible = hasFriendCode;
        _friendCodeValue.Text = hasFriendCode ? $"Steam好友号: {context.FriendCode}" : string.Empty;
        _versionValue.Text = GetVersionText(context);
        _keyInput.Text = context.DetectedKey ?? string.Empty;
        _keyStatus.Status = context.KeyValidated;

        _tileMatrix.SetTranslateEnabled(_translateCheck.Checked);
        _tileMatrix.SetSaves(context.Saves);
        _tileMatrix.SetMultiSelect(_multiSelectCheckBox.Checked);

        _conflictList.Items.Clear();
        _syncList.Items.Clear();
        _conflictList.Items.Add("点击“刷新”检查冲突状态");
        _syncList.Items.Add("点击“刷新”检查待同步状态");
        _conflictStatus.Status = true;
        _syncStatus.Status = true;
        _conflicts = Array.Empty<SaveSyncIssue>();
        _pendingSync = Array.Empty<SaveSyncIssue>();
        _conflictResolveButton.Enabled = false;
        _syncResolveButton.Enabled = false;
        UpdateDetail(Array.Empty<SaveFileEntry>());
        _tileMatrix.SetPendingSaves(_pendingDrafts.Select(x => x.FullPath).ToArray());
    }

    public void SetDiagnostics(IReadOnlyList<SaveSyncIssue> issues)
    {
        _conflicts = issues.Where(x => x.Type == SaveSyncIssueType.Conflict).ToList();
        _pendingSync = issues.Where(x => x.Type is SaveSyncIssueType.PendingSyncToBackup or SaveSyncIssueType.MissingBackup).ToList();
        RefreshIssueLists();
    }

    public void SetPendingDrafts(IReadOnlyList<SaveFileEntry> pendingDrafts)
    {
        _pendingDrafts = pendingDrafts
            .GroupBy(x => NormalizePath(x.FullPath), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();

        _tileMatrix.SetPendingSaves(_pendingDrafts.Select(x => x.FullPath).ToArray());
        RefreshIssueLists();
    }

    public (int SuggestedSlot, int? SuggestedDlc) GetSuggestedNewSaveInfo()
    {
        var first = _selected.FirstOrDefault();
        if (first is not null && !first.IsMeta)
        {
            return (first.Slot + 1, first.DlcId);
        }

        if (Context is not null)
        {
            var probe = Context.Saves.FirstOrDefault(x => !x.IsMeta);
            if (probe is not null)
            {
                var maxSlot = Context.Saves
                    .Where(x => !x.IsMeta && SaveDisplayConfig.Instance.GetGroupKey(x) == SaveDisplayConfig.Instance.GetGroupKey(probe))
                    .Select(x => x.Slot)
                    .DefaultIfEmpty(0)
                    .Max();
                return (maxSlot + 1, probe.DlcId);
            }
        }

        return (0, null);
    }

    public IReadOnlyList<SaveFileEntry> GetSelectedSaves()
    {
        return _selected;
    }

    private Control BuildTopInfoPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Padding = new Padding(10, 10, 10, 6),
            BackColor = BackColor,
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var pathRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
        };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        pathRow.Controls.Add(new Label { Text = "路径:", AutoSize = true, Margin = new Padding(0, 8, 8, 0) }, 0, 0);

        _pathInput = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
        pathRow.Controls.Add(_pathInput, 1, 0);

        var copyPath = BuildBlueButton("复制路径");
        copyPath.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_pathInput.Text))
            {
                Clipboard.SetText(_pathInput.Text);
            }
        };
        pathRow.Controls.Add(copyPath, 2, 0);

        var openExplorer = BuildBlueButton("在资源管理器打开");
        openExplorer.Click += (_, _) =>
        {
            if (Directory.Exists(_pathInput.Text))
            {
                Process.Start("explorer.exe", _pathInput.Text);
            }
        };
        pathRow.Controls.Add(openExplorer, 3, 0);
        panel.Controls.Add(pathRow, 0, 0);

        var actionRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
        };
        var openBackup = BuildBlueButton("查看备份文件");
        openBackup.Click += (_, _) => OpenPackageBackupFolder();
        actionRow.Controls.Add(openBackup);

        var addSave = BuildBlueButton("新建存档");
        addSave.Click += (_, _) => AddSaveRequested?.Invoke(this, EventArgs.Empty);
        actionRow.Controls.Add(addSave);

        var editMeta = BuildBlueButton("修改存档元数据");
        editMeta.Click += (_, _) => EditMetaRequested?.Invoke(this, EventArgs.Empty);
        actionRow.Controls.Add(editMeta);
        panel.Controls.Add(actionRow, 0, 1);

        var optionRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
        };
        optionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        optionRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var leftOptions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0),
        };

        _multiSelectCheckBox = new CheckBox
        {
            Text = "多选模式（按住 Ctrl 临时开启）",
            AutoSize = true,
            Margin = new Padding(0),
            BackColor = Color.Transparent,
        };
        _multiSelectCheckBox.CheckedChanged += (_, _) => _tileMatrix.SetMultiSelect(_multiSelectCheckBox.Checked);

        _translateCheck = new CheckBox
        {
            Text = "翻译",
            AutoSize = true,
            Checked = true,
            Margin = new Padding(8, 0, 0, 0),
            BackColor = Color.Transparent,
        };
        _translateCheck.CheckedChanged += (_, _) =>
        {
            _tileMatrix.SetTranslateEnabled(_translateCheck.Checked);
            UpdateDetail(_selected);
        };

        leftOptions.Controls.Add(_multiSelectCheckBox);
        leftOptions.Controls.Add(_translateCheck);
        optionRow.Controls.Add(leftOptions, 0, 0);

        var rightInfo = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(12, 1, 0, 0),
            Padding = new Padding(0, 1, 0, 2),
        };
        _versionValue = new Label { AutoSize = true, Text = "存档包版本: -", Margin = new Padding(0, 4, 10, 0) };
        rightInfo.Controls.Add(_versionValue);
        _friendCodeValue = new Label { AutoSize = true, Text = string.Empty, Visible = false, Margin = new Padding(0, 4, 0, 0) };
        rightInfo.Controls.Add(_friendCodeValue);
        optionRow.Controls.Add(rightInfo, 1, 0);

        panel.Controls.Add(optionRow, 0, 2);

        return panel;
    }

    private Control BuildMiddleSplitPanel()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.None,
            SplitterWidth = 8,
            BackColor = BackColor,
        };
        split.HandleCreated += (_, _) => EnsureSplitDistance(split);
        split.SizeChanged += (_, _) => EnsureSplitDistance(split);
        split.SplitterMoved += (_, _) => _splitUserResized = true;

        _tileMatrix = new SaveTileMatrixView();
        _tileMatrix.SelectionChanged += (_, selected) => UpdateDetail(selected);
        _tileMatrix.TileDoubleClicked += (_, save) => EditRequested?.Invoke(this, save);
        _tileMatrix.MoveRequested += (_, request) => MovePositionRequested?.Invoke(this, request);
        split.Panel1.Controls.Add(_tileMatrix);

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8),
            BackColor = Color.FromArgb(250, 252, 255),
        };
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        split.Panel2.Controls.Add(right);

        var keyPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
        };
        keyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        keyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        keyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        keyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        keyPanel.Controls.Add(new Label { Text = "密钥:", AutoSize = true, Margin = new Padding(0, 8, 8, 0) }, 0, 0);
        _keyInput = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "输入密钥，或留空自动检测" };
        keyPanel.Controls.Add(_keyInput, 1, 0);

        var keyApply = BuildBlueButton("应用");
        keyApply.Click += (_, _) => ApplyKeyRequested?.Invoke(this, _keyInput.Text.Trim());
        keyPanel.Controls.Add(keyApply, 2, 0);

        _keyStatus = new StatusIndicatorControl { Margin = new Padding(8, 5, 0, 0) };
        keyPanel.Controls.Add(_keyStatus, 3, 0);
        right.Controls.Add(keyPanel, 0, 0);

        (_conflictList, _conflictStatus, _conflictRefreshButton, _conflictResolveButton) =
            BuildCheckListPanel(right, "冲突（源文件与备份不一致/档位重复）", "处理冲突");
        _conflictRefreshButton.Click += (_, _) => RefreshDiagnosticsRequested?.Invoke(this, EventArgs.Empty);
        _conflictResolveButton.Click += (_, _) => ResolveConflictsRequested?.Invoke(this, _conflicts);

        (_syncList, _syncStatus, _syncRefreshButton, _syncResolveButton) =
            BuildCheckListPanel(right, "待同步（编辑未写回/尚未备份）", "批量备份待同步项");
        _syncRefreshButton.Click += (_, _) => RefreshDiagnosticsRequested?.Invoke(this, EventArgs.Empty);
        _syncResolveButton.Click += (_, _) => ResolvePendingSyncRequested?.Invoke(this, _pendingSync);
        return split;
    }

    private void EnsureSplitDistance(SplitContainer split)
    {
        if (split.Width <= 0)
        {
            return;
        }

        if (_splitInitialized || _splitUserResized)
        {
            return;
        }

        var minLeft = 420;
        var minRight = 280;
        var maxLeft = Math.Max(minLeft, split.Width - minRight);
        var target = Math.Clamp((int)(split.Width * 0.68), minLeft, maxLeft);
        if (target > 0 && target < split.Width)
        {
            try
            {
                split.SplitterDistance = target;
                _splitInitialized = true;
            }
            catch
            {
                // ignore
            }
        }
    }

    private Control BuildBottomPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            ColumnCount = 1,
            Padding = new Padding(10, 6, 10, 10),
            BackColor = BackColor,
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _detailLabel = new Label
        {
            AutoSize = true,
            Text = "未选择存档",
            MaximumSize = new Size(1400, 0),
        };
        panel.Controls.Add(_detailLabel, 0, 0);

        var timelineRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
        };
        _timelineButton = BuildBlueButton("历史版本");
        _timelineButton.Click += (_, _) =>
        {
            if (_selected.Count == 1)
            {
                TimelineRequested?.Invoke(this, _selected[0]);
                return;
            }

            MessageBox.Show("请先单选一个存档。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        timelineRow.Controls.Add(_timelineButton);
        panel.Controls.Add(timelineRow, 0, 1);

        var actionRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
        };

        _deleteButton = BuildDangerButton("删除");
        _deleteButton.Click += (_, _) => DeleteRequested?.Invoke(this, _selected);
        actionRow.Controls.Add(_deleteButton);

        _moveButton = BuildBlueButton("移动到目标包");
        _moveButton.Click += (_, _) => TransferRequested?.Invoke(this, new TransferRequest(_selected, Move: true));
        actionRow.Controls.Add(_moveButton);

        _copyButton = BuildBlueButton("复制到目标包");
        _copyButton.Click += (_, _) => TransferRequested?.Invoke(this, new TransferRequest(_selected, Move: false));
        actionRow.Controls.Add(_copyButton);

        _syncButton = BuildBlueButton("同步更改到源文件");
        _syncButton.Click += (_, _) => SyncToSourceRequested?.Invoke(this, _selected);
        actionRow.Controls.Add(_syncButton);

        _editButton = BuildBlueButton("编辑存档");
        _editButton.Click += (_, _) =>
        {
            if (_selected.Count == 1)
            {
                EditRequested?.Invoke(this, _selected[0]);
            }
        };
        actionRow.Controls.Add(_editButton);

        _viewSelectedBackupButton = BuildBlueButton("查看选中备份");
        _viewSelectedBackupButton.Click += (_, _) => OpenSelectedBackupFolder();
        actionRow.Controls.Add(_viewSelectedBackupButton);

        panel.Controls.Add(actionRow, 0, 2);
        return panel;
    }

    private static (ListBox List, StatusIndicatorControl Status, Button RefreshButton, Button ResolveButton)
        BuildCheckListPanel(TableLayoutPanel parent, string title, string resolveText)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            Margin = new Padding(0, 8, 0, 0),
            BackColor = Color.White,
            Padding = new Padding(6),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(new Label { Text = title, AutoSize = true }, 0, 0);
        var status = new StatusIndicatorControl { Status = true };
        panel.Controls.Add(status, 1, 0);

        var refresh = BuildBlueButton("刷新");
        panel.Controls.Add(refresh, 2, 0);

        var resolve = BuildBlueButton(resolveText);
        panel.Controls.Add(resolve, 3, 0);

        var list = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            HorizontalScrollbar = true,
        };
        panel.Controls.Add(list, 0, 1);
        panel.SetColumnSpan(list, 4);
        parent.Controls.Add(panel);
        return (list, status, refresh, resolve);
    }

    private void RefreshIssueLists()
    {
        _conflictList.Items.Clear();
        if (_conflicts.Count == 0)
        {
            _conflictList.Items.Add("未发现冲突。");
        }
        else
        {
            _conflictList.Items.AddRange(_conflicts.Select(x => "[冲突] " + x.Message).ToArray());
        }

        var syncMessages = new List<string>();
        foreach (var save in _pendingDrafts)
        {
            syncMessages.Add($"[编辑待同步] {save.FileName}: 已修改但未写回源文件，点击“同步更改到源文件”生效。");
        }

        foreach (var issue in _pendingSync)
        {
            syncMessages.Add("[备份待同步] " + issue.Message);
        }

        _syncList.Items.Clear();
        if (syncMessages.Count == 0)
        {
            _syncList.Items.Add("未发现待同步项。");
        }
        else
        {
            _syncList.Items.AddRange(syncMessages.ToArray());
        }

        _conflictStatus.Status = _conflicts.Count == 0;
        _syncStatus.Status = syncMessages.Count == 0;
        _conflictResolveButton.Enabled = _conflicts.Count > 0;
        _syncResolveButton.Enabled = _pendingSync.Count > 0;

        UpdateHorizontalExtent(_conflictList);
        UpdateHorizontalExtent(_syncList);
    }

    private void UpdateDetail(IReadOnlyList<SaveFileEntry> selected)
    {
        _selected = selected;
        if (selected.Count == 0)
        {
            _detailLabel.Text = "未选择存档";
            _timelineButton.Visible = false;
            ToggleActionButtons(false, false);
            return;
        }

        if (selected.Count == 1)
        {
            var s = selected[0];
            var groupKey = SaveDisplayConfig.Instance.GetGroupKey(s);
            var groupText = SaveDisplayConfig.Instance.GetGroupDisplayName(groupKey, _translateCheck.Checked);
            var pendingMark = _pendingDrafts.Any(x => PathsEqual(x.FullPath, s.FullPath)) ? "\n状态: 已编辑，待同步到源文件" : string.Empty;
            _detailLabel.Text =
                $"文件: {s.FileName}\n分组: {groupText} ({groupKey}) | 槽位: {(s.IsMeta ? "-" : (s.Slot + 1).ToString())}\n大小: {FormatSize(s.Size)} | 修改: {s.LastWriteTime:yyyy-MM-dd HH:mm:ss}{pendingMark}";
            _timelineButton.Visible = true;
            ToggleActionButtons(true, true);
            return;
        }

        _detailLabel.Text = $"已选中 {selected.Count} 个存档";
        _timelineButton.Visible = false;
        ToggleActionButtons(true, false);
    }

    private void ToggleActionButtons(bool hasSelection, bool singleSelection)
    {
        _copyButton.Enabled = hasSelection;
        _moveButton.Enabled = hasSelection;
        _syncButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
        _editButton.Enabled = singleSelection;
        _viewSelectedBackupButton.Enabled = hasSelection;
        _timelineButton.Enabled = singleSelection;
    }

    private void OpenPackageBackupFolder()
    {
        if (Context is null)
        {
            return;
        }

        var backupRoot = Path.Combine(Context.PackagePath, ".overcookedtool-backup");
        Directory.CreateDirectory(backupRoot);
        Process.Start("explorer.exe", backupRoot);
    }

    private void OpenSelectedBackupFolder()
    {
        var first = _selected.FirstOrDefault();
        if (first is null)
        {
            return;
        }

        var dir = Path.GetDirectoryName(first.FullPath);
        if (string.IsNullOrWhiteSpace(dir))
        {
            return;
        }

        var backupRoot = Path.Combine(dir, ".overcookedtool-backup");
        Directory.CreateDirectory(backupRoot);
        Process.Start("explorer.exe", backupRoot);
    }

    private static Button BuildBlueButton(string text)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(227, 242, 253),
            ForeColor = Color.FromArgb(13, 71, 161),
            Margin = new Padding(6, 2, 0, 2),
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(144, 202, 249);
        button.EnabledChanged += (_, _) => ApplyButtonStateStyle(button, danger: false);
        ApplyButtonStateStyle(button, danger: false);
        return button;
    }

    private static Button BuildDangerButton(string text)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(244, 67, 54),
            ForeColor = Color.White,
            Margin = new Padding(6, 2, 0, 2),
        };
        button.EnabledChanged += (_, _) => ApplyButtonStateStyle(button, danger: true);
        ApplyButtonStateStyle(button, danger: true);
        return button;
    }

    private static void ApplyButtonStateStyle(Button button, bool danger)
    {
        if (button.Enabled)
        {
            if (danger)
            {
                button.BackColor = Color.FromArgb(244, 67, 54);
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = Color.FromArgb(229, 57, 53);
            }
            else
            {
                button.BackColor = Color.FromArgb(227, 242, 253);
                button.ForeColor = Color.FromArgb(13, 71, 161);
                button.FlatAppearance.BorderColor = Color.FromArgb(144, 202, 249);
            }
        }
        else
        {
            button.BackColor = Color.FromArgb(236, 239, 241);
            button.ForeColor = Color.FromArgb(158, 158, 158);
            button.FlatAppearance.BorderColor = Color.FromArgb(207, 216, 220);
        }
    }

    private static string GetVersionText(SavePackageContext context)
    {
        var tag = context.Version switch
        {
            SaveVersion.Ayce => "OcA",
            SaveVersion.Oc2 => "Oc2",
            _ => context.Platform switch
            {
                SavePlatform.AyceJson => "OcA",
                SavePlatform.Oc2Binary => "Oc2",
                SavePlatform.SwitchJson => "SwitchJson",
                SavePlatform.XboxBinary => "Xbox",
                _ => "Unknown",
            },
        };

        return $"存档包版本: {tag}";
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:F1} KB";
        }

        return $"{bytes / 1024.0 / 1024.0:F1} MB";
    }

    private static void UpdateHorizontalExtent(ListBox list)
    {
        var maxWidth = 0;
        using var g = list.CreateGraphics();
        foreach (var item in list.Items.Cast<object>())
        {
            var width = TextRenderer.MeasureText(g, item.ToString(), list.Font).Width + 12;
            if (width > maxWidth)
            {
                maxWidth = width;
            }
        }

        list.HorizontalExtent = maxWidth;
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool PathsEqual(string a, string b)
    {
        return string.Equals(NormalizePath(a), NormalizePath(b), StringComparison.OrdinalIgnoreCase);
    }
}
