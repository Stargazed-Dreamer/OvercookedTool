using System.Diagnostics;
using OvercookedTool.Core.Logging;
using OvercookedTool.Core.Models;
using OvercookedTool.Core.Services;

namespace OvercookedTool.App;

internal sealed class MainForm : Form
{
    private readonly SavePackageService _saveService = new();
    private readonly Dictionary<string, TabPage> _tabsByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, PendingSaveEdit>> _pendingEditsByPackage = new(StringComparer.OrdinalIgnoreCase);
    private readonly AppSettings _settings;

    private readonly TabControl _tabControl;
    private readonly TabPage _plusTab;
    private readonly ToolStripStatusLabel _statusLabel;
    private DateTime _lastStatusUpdate = DateTime.MinValue;
    private bool _handlingPlusSelection;
    private bool _isMovingWindow;
    private int _lastNormalTabIndex;

    public MainForm(AppSettings? startupSettings = null)
    {
        _settings = startupSettings ?? AppSettingsStore.Load();
        _saveService.BackupHistoryPerSave = Math.Clamp(_settings.MaxBackupPerSave, 1, 50);

        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        UpdateStyles();

        Text = "胡闹厨房存档管理器";
        Width = 1380;
        Height = 900;
        MinimumSize = new Size(960, 700);
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;
        KeyPreview = true;
        BackColor = Color.FromArgb(244, 248, 255);
        UiPerformance.EnableDoubleBuffer(this);

        var menu = BuildMenu();
        MainMenuStrip = menu;

        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            DrawMode = TabDrawMode.OwnerDrawFixed,
            Padding = new Point(18, 6),
        };
        UiPerformance.EnableDoubleBuffer(_tabControl);
        _tabControl.DrawItem += TabControl_DrawItem;
        _tabControl.MouseDown += TabControl_MouseDown;
        _tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;

        _plusTab = CreatePlusTab();
        _tabControl.TabPages.Add(_plusTab);

        var statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel("就绪");
        statusStrip.Items.Add(_statusLabel);

        Controls.Add(_tabControl);
        Controls.Add(statusStrip);
        Controls.Add(menu);

        DragEnter += MainForm_DragEnter;
        DragDrop += MainForm_DragDrop;
        AppLogger.LogEmitted += OnLogEmitted;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        AppLogger.LogEmitted -= OnLogEmitted;
        AppSettingsStore.Save(_settings);
        base.OnFormClosed(e);
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip
        {
            BackColor = Color.FromArgb(236, 244, 255),
        };

        var file = new ToolStripMenuItem("文件");
        file.DropDownItems.Add("导入存档包...", null, (_, _) => OpenPackageByDialog());
        file.DropDownItems.Add("关闭当前标签", null, (_, _) => CloseCurrentTab());
        file.DropDownItems.Add("退出", null, (_, _) => Close());
        menu.Items.Add(file);

        var settingsItem = new ToolStripMenuItem("设置");
        settingsItem.Click += (_, _) => ShowSettings();
        menu.Items.Add(settingsItem);

        var about = new ToolStripMenuItem("关于");
        about.DropDownItems.Add("关于程序", null, (_, _) =>
        {
            using var form = new AboutForm();
            form.ShowDialog(this);
        });
        about.DropDownItems.Add("打开日志目录", null, (_, _) => OpenLogDirectory());
        about.DropDownItems.Add("打赏", null, (_, _) =>
        {
            using var donate = new DonateForm();
            donate.ShowDialog(this);
        });
        menu.Items.Add(about);

        return menu;
    }

    private void OpenLogDirectory()
    {
        var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDir);
        Process.Start("explorer.exe", logDir);
    }

    private void ShowSettings()
    {
        using var dialog = new SettingsForm(_settings);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _settings.EnableAutoDetectOnImport = dialog.EnableAutoDetectOnImport;
        _settings.MaxRecentCount = dialog.MaxRecentCount;
        _settings.EnableLogging = dialog.EnableLogging;
        _settings.MaxBackupPerSave = dialog.MaxBackupPerSave;
        _saveService.BackupHistoryPerSave = _settings.MaxBackupPerSave;
        AppLogger.SetEnabled(_settings.EnableLogging);

        while (_settings.RecentPackagePaths.Count > _settings.MaxRecentCount)
        {
            _settings.RecentPackagePaths.RemoveAt(_settings.RecentPackagePaths.Count - 1);
        }

        AppSettingsStore.Save(_settings);
        _statusLabel.Text = "设置已保存";
    }

    private TabPage CreatePlusTab()
    {
        var page = new TabPage("+");
        var label = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "点击此空白区域导入存档包\n或拖入存档文件夹/.save 文件",
            Font = new Font("Segoe UI", 13, FontStyle.Regular),
            ForeColor = Color.FromArgb(110, 110, 110),
            Cursor = Cursors.Hand,
            BackColor = Color.FromArgb(250, 252, 255),
        };
        label.Click += (_, _) => OpenPackageByDialog();
        page.Click += (_, _) => OpenPackageByDialog();
        page.Controls.Add(label);
        return page;
    }

    private void OpenPackageByDialog()
    {
        using var dialog = new ImportPackageDialog(_saveService, _settings);
        if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            return;
        }

        OpenByPotentialRoot(dialog.SelectedPath!);
    }

    private void OpenByPotentialRoot(string inputPath)
    {
        if (_saveService.TryResolvePackagePath(inputPath, out var resolved, out var candidates))
        {
            OpenOrReloadPackage(resolved, preferredKey: null);
            return;
        }

        if (candidates.Count > 1)
        {
            using var choose = new SelectPackageDialog(
                "选择存档包",
                "该目录下检测到多个候选存档包，请选择一个导入：",
                candidates);
            if (choose.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(choose.SelectedPath))
            {
                OpenOrReloadPackage(choose.SelectedPath!, preferredKey: null);
                return;
            }
        }

        MessageBox.Show("目录中没有可识别存档包。", "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void OpenOrReloadPackage(string packagePath, string? preferredKey)
    {
        try
        {
            var context = _saveService.LoadPackage(packagePath, string.IsNullOrWhiteSpace(preferredKey) ? null : preferredKey);
            if (_tabsByPath.TryGetValue(packagePath, out var existing))
            {
                if (existing.Controls.OfType<PackageTabView>().FirstOrDefault() is { } existingView)
                {
                    existingView.Bind(context);
                    ApplyPendingStateToView(existingView);
                    RefreshDiagnostics(existingView);
                }

                existing.Text = context.DisplayName;
                _tabControl.SelectedTab = existing;
                RememberPackage(context.PackagePath);
                return;
            }

            var page = new TabPage(context.DisplayName);
            var view = new PackageTabView();
            view.Bind(context);
            view.ApplyKeyRequested += (_, key) => OpenOrReloadPackage(packagePath, key);
            view.AddSaveRequested += (_, _) => HandleAddSave(view);
            view.EditMetaRequested += (_, _) => HandleEditMeta(view);
            view.TransferRequested += (_, request) => HandleTransfer(view, request);
            view.DeleteRequested += (_, saves) => HandleDelete(view, saves);
            view.SyncToSourceRequested += (_, saves) => HandleSyncToSource(view, saves);
            view.EditRequested += (_, save) => HandleEdit(view, save);
            view.MovePositionRequested += (_, request) => HandleMovePosition(view, request);
            view.TimelineRequested += (_, save) => HandleTimeline(view, save);
            view.RefreshDiagnosticsRequested += (_, _) => RefreshDiagnostics(view);
            view.ResolveConflictsRequested += (_, issues) => HandleResolveConflicts(view, issues);
            view.ResolvePendingSyncRequested += (_, issues) => HandleResolvePendingSync(view, issues);
            page.Controls.Add(view);

            _tabsByPath[packagePath] = page;
            _tabControl.TabPages.Insert(Math.Max(_tabControl.TabPages.Count - 1, 0), page);
            _tabControl.SelectedTab = page;
            _lastNormalTabIndex = _tabControl.SelectedIndex;
            RememberPackage(context.PackagePath);
            ApplyPendingStateToView(view);
            RefreshDiagnostics(view);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Open package failed: {packagePath}", ex);
            MessageBox.Show(ex.Message, "打开失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshDiagnostics(PackageTabView view)
    {
        if (view.Context is null)
        {
            return;
        }

        var issues = _saveService.AnalyzeSyncIssues(view.Context);
        view.SetDiagnostics(issues);
        ApplyPendingStateToView(view);
    }

    private void ApplyPendingStateToView(PackageTabView view)
    {
        if (view.Context is null)
        {
            view.SetPendingDrafts(Array.Empty<SaveFileEntry>());
            return;
        }

        var packageKey = NormalizePath(view.Context.PackagePath);
        if (!_pendingEditsByPackage.TryGetValue(packageKey, out var map) || map.Count == 0)
        {
            view.SetPendingDrafts(Array.Empty<SaveFileEntry>());
            return;
        }

        var existingByPath = view.Context.Saves.ToDictionary(x => NormalizePath(x.FullPath), x => x, StringComparer.OrdinalIgnoreCase);
        foreach (var stale in map.Keys.Where(k => !existingByPath.ContainsKey(k)).ToList())
        {
            map.Remove(stale);
        }

        if (map.Count == 0)
        {
            _pendingEditsByPackage.Remove(packageKey);
            view.SetPendingDrafts(Array.Empty<SaveFileEntry>());
            return;
        }

        var pendingSaves = map.Keys
            .Where(existingByPath.ContainsKey)
            .Select(k => existingByPath[k])
            .ToList();
        view.SetPendingDrafts(pendingSaves);
    }

    private void HandleAddSave(PackageTabView view)
    {
        if (view.Context is null)
        {
            return;
        }

        var (slot, dlc) = view.GetSuggestedNewSaveInfo();
        using var dialog = new AddSaveDialog(slot, dlc);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var template = view.GetSelectedSaves().FirstOrDefault(x => !x.IsMeta)
            ?? view.Context.Saves.FirstOrDefault(x => !x.IsMeta && (dialog.DlcId == null || x.DlcId == dialog.DlcId));
        var result = _saveService.CreateSaveWithPreset(view.Context, dialog.Slot, dialog.DlcId, dialog.Preset, template);
        MessageBox.Show(result.Message, result.Success ? "创建完成" : "创建失败", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        if (result.Success)
        {
            OpenOrReloadPackage(view.Context.PackagePath, view.ManualKey);
        }
    }

    private void HandleEditMeta(PackageTabView view)
    {
        if (view.Context is null)
        {
            return;
        }

        var meta = view.Context.Saves.FirstOrDefault(x => x.IsMeta);
        if (meta is null)
        {
            MessageBox.Show("当前存档包没有 Meta 存档。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var json = TryGetPendingJson(view.Context.PackagePath, meta.FullPath, out var pendingJson)
                ? pendingJson
                : _saveService.ReadSaveAsJson(view.Context, meta);
            var version = SaveJsonConverter.DetectVersion(json);
            using var editor = new MetaTableEditorForm(meta, version, json, view.TranslateEnabled);
            if (editor.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            PutPendingEdit(view.Context.PackagePath, meta, editor.JsonText);
            ApplyPendingStateToView(view);
            RefreshDiagnostics(view);
            MessageBox.Show("Meta 修改已暂存，点击“同步更改到源文件”后才会真正写入。", "已暂存", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Edit meta failed.", ex);
            MessageBox.Show(ex.Message, "编辑失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void HandleTransfer(PackageTabView view, TransferRequest request)
    {
        if (view.Context is null || request.Saves.Count == 0)
        {
            MessageBox.Show("请先选择至少一个存档。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var pendingCount = request.Saves.Count(s => HasPendingEdit(view.Context.PackagePath, s.FullPath));
        if (pendingCount > 0)
        {
            var ask = MessageBox.Show(
                $"选中的存档中有 {pendingCount} 个存在未同步编辑。\n继续复制/移动将按当前源文件内容执行，暂存编辑不会自动带出。\n是否继续？",
                "存在待同步编辑",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (ask != DialogResult.Yes)
            {
                return;
            }
        }

        var targetCandidates = _tabsByPath.Keys
            .Where(x => !string.Equals(x, view.Context.PackagePath, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x)
            .ToList();
        if (targetCandidates.Count == 0)
        {
            MessageBox.Show("请先在本工具中打开至少一个目标存档包。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var choose = new SelectPackageDialog(
            request.Move ? "选择移动目标包" : "选择复制目标包",
            "请选择已加载的目标存档包：",
            targetCandidates);
        if (choose.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(choose.SelectedPath))
        {
            return;
        }

        var target = choose.SelectedPath!;
        var fail = 0;
        var ok = 0;
        var errors = new List<string>();
        foreach (var save in request.Saves)
        {
            var result = _saveService.TransferSave(view.Context, save, target, request.Move);
            if (result.Success)
            {
                ok++;
                if (request.Move)
                {
                    RemovePendingEdit(view.Context.PackagePath, save.FullPath);
                }
            }
            else
            {
                fail++;
                errors.Add($"{save.FileName}: {result.Message}");
            }
        }

        var summary = fail == 0
            ? $"完成：成功 {ok} 项。"
            : $"完成：成功 {ok} 项，失败 {fail} 项。\n{string.Join("\n", errors.Take(5))}";
        MessageBox.Show(summary, "执行结果", MessageBoxButtons.OK, fail == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        OpenOrReloadPackage(view.Context.PackagePath, view.ManualKey);
        OpenOrReloadPackage(target, null);
    }

    private void HandleDelete(PackageTabView view, IReadOnlyList<SaveFileEntry> saves)
    {
        if (view.Context is null || saves.Count == 0)
        {
            MessageBox.Show("请先选择至少一个存档。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"确定删除 {saves.Count} 个存档吗？\n删除前会自动备份。",
            "确认删除",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        var result = _saveService.DeleteSaves(view.Context, saves);
        foreach (var save in saves)
        {
            RemovePendingEdit(view.Context.PackagePath, save.FullPath);
        }

        MessageBox.Show(result.Message, result.Success ? "删除完成" : "删除失败", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        if (result.Success)
        {
            OpenOrReloadPackage(view.Context.PackagePath, view.ManualKey);
        }
    }

    private void HandleSyncToSource(PackageTabView view, IReadOnlyList<SaveFileEntry> saves)
    {
        if (view.Context is null || saves.Count == 0)
        {
            MessageBox.Show("请先选择至少一个存档。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var packageKey = NormalizePath(view.Context.PackagePath);
        if (!_pendingEditsByPackage.TryGetValue(packageKey, out var map) || map.Count == 0)
        {
            MessageBox.Show("所选存档没有待同步编辑。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var ok = 0;
        var fail = 0;
        var errors = new List<string>();

        foreach (var save in saves)
        {
            var key = NormalizePath(save.FullPath);
            if (!map.TryGetValue(key, out var pending))
            {
                continue;
            }

            try
            {
                _saveService.WriteJsonToSave(view.Context, save, pending.JsonText, "edit-sync");
                map.Remove(key);
                ok++;
            }
            catch (Exception ex)
            {
                fail++;
                errors.Add($"{save.FileName}: {ex.Message}");
            }
        }

        if (map.Count == 0)
        {
            _pendingEditsByPackage.Remove(packageKey);
        }

        if (ok == 0 && fail == 0)
        {
            MessageBox.Show("所选存档没有待同步编辑。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var msg = fail == 0
            ? $"同步完成：{ok} 项已写回源文件。"
            : $"同步完成：成功 {ok}，失败 {fail}。\n{string.Join("\n", errors.Take(5))}";
        MessageBox.Show(msg, fail == 0 ? "同步完成" : "同步结果", MessageBoxButtons.OK, fail == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        OpenOrReloadPackage(view.Context.PackagePath, view.ManualKey);
    }

    private void HandleEdit(PackageTabView view, SaveFileEntry save)
    {
        if (view.Context is null)
        {
            return;
        }

        try
        {
            var json = TryGetPendingJson(view.Context.PackagePath, save.FullPath, out var pendingJson)
                ? pendingJson
                : _saveService.ReadSaveAsJson(view.Context, save);
            var version = SaveJsonConverter.DetectVersion(json);
            using var editor = new SaveTableEditorForm(save, version, json, view.TranslateEnabled);
            if (editor.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            PutPendingEdit(view.Context.PackagePath, save, editor.JsonText);
            ApplyPendingStateToView(view);
            RefreshDiagnostics(view);
            AppLogger.Info($"Save edit staged: {save.FullPath}");
            MessageBox.Show("修改已暂存，点击“同步更改到源文件”后才会真正写入。", "已暂存", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Edit save failed.", ex);
            MessageBox.Show(ex.Message, "编辑失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void HandleTimeline(PackageTabView view, SaveFileEntry save)
    {
        if (view.Context is null)
        {
            return;
        }

        var history = _saveService.GetBackupHistory(save);
        using var timeline = new SaveTimelineForm(save.FileName, history);
        if (timeline.ShowDialog(this) != DialogResult.OK || timeline.SelectedBackup is null)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"确认将 {save.FileName} 恢复到所选历史版本？\n恢复前会自动备份当前文件。",
            "确认恢复",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        var result = _saveService.RestoreBackup(save, timeline.SelectedBackup.BackupPath);
        MessageBox.Show(result.Message, result.Success ? "恢复完成" : "恢复失败", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        if (result.Success)
        {
            OpenOrReloadPackage(view.Context.PackagePath, view.ManualKey);
        }
    }

    private void HandleResolveConflicts(PackageTabView view, IReadOnlyList<SaveSyncIssue> issues)
    {
        if (view.Context is null || issues.Count == 0)
        {
            MessageBox.Show("当前没有可处理冲突。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var choice = MessageBox.Show(
            "Yes = 保留源文件（覆盖备份）\nNo = 保留备份文件（覆盖源文件）",
            "冲突处理策略",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);
        if (choice == DialogResult.Cancel)
        {
            return;
        }

        var keepSource = choice == DialogResult.Yes;
        var result = _saveService.ResolveConflicts(issues, keepSource);
        MessageBox.Show(result.Message, result.Success ? "处理完成" : "处理结果", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        OpenOrReloadPackage(view.Context.PackagePath, view.ManualKey);
    }

    private void HandleResolvePendingSync(PackageTabView view, IReadOnlyList<SaveSyncIssue> issues)
    {
        if (view.Context is null || issues.Count == 0)
        {
            MessageBox.Show("当前没有“备份待同步”项。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var saves = issues.Select(x => x.Save).DistinctBy(x => x.FullPath).ToList();
        var result = _saveService.BackupSaves(saves);
        MessageBox.Show(result.Message, result.Success ? "处理完成" : "处理结果", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        OpenOrReloadPackage(view.Context.PackagePath, view.ManualKey);
    }

    private void HandleMovePosition(PackageTabView view, MovePositionRequest request)
    {
        if (view.Context is null)
        {
            return;
        }

        var result = _saveService.MoveSavePosition(view.Context, request.Save, request.Direction);
        if (!result.Success)
        {
            MessageBox.Show(result.Message, "移动失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.TargetPath))
        {
            MovePendingEditPath(view.Context.PackagePath, request.Save.FullPath, result.TargetPath!);
        }

        OpenOrReloadPackage(view.Context.PackagePath, view.ManualKey);
    }

    private void RememberPackage(string packagePath)
    {
        AppSettingsStore.PushRecent(_settings, packagePath);
        AppSettingsStore.Save(_settings);
    }

    private void CloseCurrentTab()
    {
        var page = _tabControl.SelectedTab;
        if (page is null || ReferenceEquals(page, _plusTab))
        {
            return;
        }

        CloseTab(page);
    }

    private void CloseTab(TabPage page)
    {
        if (page.Controls.OfType<PackageTabView>().FirstOrDefault() is { Context: not null } view)
        {
            _tabsByPath.Remove(view.Context.PackagePath);
        }

        _tabControl.TabPages.Remove(page);
        page.Dispose();
        _tabControl.SelectedTab = _tabControl.TabPages.Count > 1 ? _tabControl.TabPages[0] : _plusTab;
    }

    private void TabControl_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_handlingPlusSelection)
        {
            return;
        }

        if (_tabControl.TabCount == 0 || _tabControl.SelectedIndex < 0 || _tabControl.SelectedIndex >= _tabControl.TabPages.Count)
        {
            return;
        }

        if (ReferenceEquals(_tabControl.SelectedTab, _plusTab))
        {
            _handlingPlusSelection = true;
            try
            {
                OpenPackageByDialog();
                if (_tabControl.TabPages.Count > 1)
                {
                    var index = Math.Min(_lastNormalTabIndex, _tabControl.TabPages.Count - 2);
                    _tabControl.SelectedIndex = Math.Max(index, 0);
                }
            }
            finally
            {
                _handlingPlusSelection = false;
            }
        }
        else if (_tabControl.SelectedIndex >= 0)
        {
            _lastNormalTabIndex = _tabControl.SelectedIndex;
        }
    }

    private void TabControl_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _tabControl.TabPages.Count)
        {
            return;
        }

        var page = _tabControl.TabPages[e.Index];
        var selected = _tabControl.SelectedIndex >= 0 && _tabControl.SelectedIndex < _tabControl.TabPages.Count
            ? _tabControl.TabPages[_tabControl.SelectedIndex]
            : null;

        var rect = _tabControl.GetTabRect(e.Index);
        using var bg = new SolidBrush(page == selected ? Color.White : Color.FromArgb(235, 241, 251));
        e.Graphics.FillRectangle(bg, rect);

        var text = page.Text;
        var textRect = new Rectangle(rect.X + 8, rect.Y + 4, rect.Width - 20, rect.Height - 8);
        TextRenderer.DrawText(e.Graphics, text, Font, textRect, Color.Black, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        if (!ReferenceEquals(page, _plusTab))
        {
            var closeRect = GetTabCloseRect(rect);
            TextRenderer.DrawText(e.Graphics, "×", Font, closeRect, Color.FromArgb(100, 100, 100), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        e.DrawFocusRectangle();
    }

    private void TabControl_MouseDown(object? sender, MouseEventArgs e)
    {
        for (var i = 0; i < _tabControl.TabPages.Count; i++)
        {
            var page = _tabControl.TabPages[i];
            if (ReferenceEquals(page, _plusTab))
            {
                continue;
            }

            var tabRect = _tabControl.GetTabRect(i);
            var closeRect = GetTabCloseRect(tabRect);
            if (closeRect.Contains(e.Location))
            {
                CloseTab(page);
                return;
            }
        }
    }

    private static Rectangle GetTabCloseRect(Rectangle tabRect)
    {
        return new Rectangle(tabRect.Right - 18, tabRect.Top + 4, 14, tabRect.Height - 8);
    }

    private void OnLogEmitted(string line)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(OnLogEmitted), line);
            return;
        }

        if (_isMovingWindow)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if ((now - _lastStatusUpdate).TotalMilliseconds < 120)
        {
            return;
        }

        _lastStatusUpdate = now;
        const int maxLen = 180;
        _statusLabel.Text = line.Length <= maxLen ? line : line[..maxLen] + "...";
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_ENTERSIZEMOVE = 0x0231;
        const int WM_EXITSIZEMOVE = 0x0232;

        if (m.Msg == WM_ENTERSIZEMOVE)
        {
            _isMovingWindow = true;
        }
        else if (m.Msg == WM_EXITSIZEMOVE)
        {
            _isMovingWindow = false;
        }

        base.WndProc(ref m);
    }

    private void MainForm_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    private void MainForm_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return;
        }

        foreach (var path in paths)
        {
            try
            {
                var packagePath = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(packagePath))
                {
                    OpenByPotentialRoot(packagePath);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Drag/drop open failed: {path}", ex);
            }
        }
    }

    private bool HasPendingEdit(string packagePath, string savePath)
    {
        return _pendingEditsByPackage.TryGetValue(NormalizePath(packagePath), out var map)
               && map.ContainsKey(NormalizePath(savePath));
    }

    private bool TryGetPendingJson(string packagePath, string savePath, out string jsonText)
    {
        jsonText = string.Empty;
        if (_pendingEditsByPackage.TryGetValue(NormalizePath(packagePath), out var map)
            && map.TryGetValue(NormalizePath(savePath), out var pending))
        {
            jsonText = pending.JsonText;
            return true;
        }

        return false;
    }

    private void PutPendingEdit(string packagePath, SaveFileEntry save, string jsonText)
    {
        var packageKey = NormalizePath(packagePath);
        if (!_pendingEditsByPackage.TryGetValue(packageKey, out var map))
        {
            map = new Dictionary<string, PendingSaveEdit>(StringComparer.OrdinalIgnoreCase);
            _pendingEditsByPackage[packageKey] = map;
        }

        var saveKey = NormalizePath(save.FullPath);
        map[saveKey] = new PendingSaveEdit
        {
            Save = save,
            JsonText = jsonText,
            UpdatedAt = DateTime.Now,
        };
    }

    private void RemovePendingEdit(string packagePath, string savePath)
    {
        var packageKey = NormalizePath(packagePath);
        if (!_pendingEditsByPackage.TryGetValue(packageKey, out var map))
        {
            return;
        }

        map.Remove(NormalizePath(savePath));
        if (map.Count == 0)
        {
            _pendingEditsByPackage.Remove(packageKey);
        }
    }

    private void MovePendingEditPath(string packagePath, string oldPath, string newPath)
    {
        var packageKey = NormalizePath(packagePath);
        if (!_pendingEditsByPackage.TryGetValue(packageKey, out var map))
        {
            return;
        }

        var oldKey = NormalizePath(oldPath);
        var newKey = NormalizePath(newPath);
        if (!map.TryGetValue(oldKey, out var pending))
        {
            return;
        }

        map.Remove(oldKey);
        var renamed = CloneWithPath(pending.Save, newPath);
        map[newKey] = pending with { Save = renamed };
    }

    private static SaveFileEntry CloneWithPath(SaveFileEntry source, string newPath)
    {
        return new SaveFileEntry
        {
            FileName = Path.GetFileName(newPath),
            FullPath = newPath,
            Size = source.Size,
            LastWriteTime = source.LastWriteTime,
            Slot = source.Slot,
            DlcId = source.DlcId,
            IsMeta = source.IsMeta,
            StarCount = source.StarCount,
            Prefix = source.Prefix,
        };
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private sealed record PendingSaveEdit
    {
        public required SaveFileEntry Save { get; init; }
        public required string JsonText { get; init; }
        public required DateTime UpdatedAt { get; init; }
    }
}


