using System.ComponentModel;
using OvercookedTool.Core.Models;

namespace OvercookedTool.App;

/// <summary>
/// 一个用于管理和显示存档瓷砖矩阵视图的用户控件，支持分组、多选、待处理状态标记和拖拽移动请求。
/// </summary>
internal sealed class SaveTileMatrixView : UserControl
{
    private readonly Panel _scrollPanel;
    private readonly FlowLayoutPanel _groupContainer;
    /// <summary> 存储所有分组面板的集合 </summary>
    private readonly List<Panel> _groupPanels = new();
    /// <summary> 存储所有瓷砖控件的集合 </summary>
    private readonly List<SaveTileControl> _tiles = new();
    /// <summary> 存储当前已选中的瓷砖控件集合 </summary>
    private readonly List<SaveTileControl> _selected = new();
    /// <summary> 存储所有存档条目的列表 </summary>
    private IReadOnlyList<SaveFileEntry> _allSaves = Array.Empty<SaveFileEntry>();
    /// <summary> 存储待处理存档路径的哈希集合，用于标记瓷砖状态 </summary>
    private readonly HashSet<string> _pendingPaths = new(StringComparer.OrdinalIgnoreCase);
    /// <summary> 标志位，指示是否启用翻译（本地化）功能 </summary>
    private bool _translateEnabled = true;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool MultiSelectMode { get; private set; }

    /// <summary> 当选择内容改变时触发的事件 </summary>
    public event EventHandler<IReadOnlyList<SaveFileEntry>>? SelectionChanged;
    /// <summary> 当瓷砖被双击时触发的事件 </summary>
    public event EventHandler<SaveFileEntry>? TileDoubleClicked;
    /// <summary> 当瓷砖请求移动位置时触发的事件 </summary>
    public event EventHandler<MovePositionRequest>? MoveRequested;

    public SaveTileMatrixView()
    {
        // 设置控件基本属性
        Dock = DockStyle.Fill;
        UiPerformance.EnableDoubleBuffer(this);
        _scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(4),
            BackColor = Color.FromArgb(247, 250, 255),
        };
        UiPerformance.EnableDoubleBuffer(_scrollPanel);
        Controls.Add(_scrollPanel);

        // 创建用于容纳所有分组的流式布局面板
        _groupContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };
        UiPerformance.EnableDoubleBuffer(_groupContainer);
        _scrollPanel.Controls.Add(_groupContainer);
        // 窗口大小改变时，重新调整分组面板的宽度
        Resize += (_, _) => ReflowGroupPanelWidths();
        _scrollPanel.Resize += (_, _) => ReflowGroupPanelWidths();
    }

    /// <summary>
    /// 获取当前选中的所有存档条目。
    /// </summary>
    /// <returns> 包含所有选中条目的只读列表 </returns>
    public IReadOnlyList<SaveFileEntry> GetSelectedEntries()
    {
        return _selected.Select(x => x.Entry).ToList();
    }

    /// <summary>
    /// 设置翻译功能是否启用，并重建视图。
    /// </summary>
    /// <param name="enabled"> 是否启用翻译 </param>
    public void SetTranslateEnabled(bool enabled)
    {
        if (_translateEnabled == enabled)
        {
            return;
        }

        _translateEnabled = enabled;
        RebuildView();
    }

    /// <summary>
    /// 设置待处理的存档路径列表，并更新对应瓷砖的状态。
    /// </summary>
    /// <param name="fullPaths"> 待处理的存档文件路径集合 </param>
    public void SetPendingSaves(IReadOnlyCollection<string> fullPaths)
    {
        // 清空并重新填充待处理路径集合
        _pendingPaths.Clear();
        foreach (var path in fullPaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                _pendingPaths.Add(NormalizePath(path));
            }
        }

        // 遍历所有瓷砖，根据其路径是否在待处理集合中，更新其状态
        foreach (var tile in _tiles)
        {
            tile.SetPending(_pendingPaths.Contains(NormalizePath(tile.Entry.FullPath)));
        }
    }

    /// <summary>
    /// 设置是否为多选模式。
    /// </summary>
    /// <param name="enabled"> 是否启用多选 </param>
    public void SetMultiSelect(bool enabled)
    {
        MultiSelectMode = enabled;
        // 更新所有瓷砖控件的多选模式
        foreach (var tile in _tiles)
        {
            tile.SetMultiSelect(enabled);
        }

        // 如果从多选模式切换到单选模式，且当前有多个选中项，则只保留第一个选中项
        if (!enabled && _selected.Count > 1)
        {
            var keep = _selected.First();
            foreach (var tile in _selected.Skip(1).ToList())
            {
                tile.SetSelected(false);
                _selected.Remove(tile);
            }

            keep.SetSelected(true);
            EmitSelectionChanged();
        }
    }

    /// <summary>
    /// 设置要显示的存档数据，并重建视图。
    /// </summary>
    /// <param name="saves"> 存档条目列表 </param>
    public void SetSaves(IReadOnlyList<SaveFileEntry> saves)
    {
        _allSaves = saves.ToList();
        RebuildView();
    }

    /// <summary>
    /// 根据当前数据和状态重建整个视图布局。
    /// </summary>
    private void RebuildView()
    {
        // 在重建前，记录当前选中的条目路径，以便在重建后恢复选中状态
        var selectedPaths = _selected.Select(x => NormalizePath(x.Entry.FullPath)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 暂停布局，提高重建性能
        SuspendLayout();
        _tiles.Clear();
        _selected.Clear();
        _groupPanels.Clear();
        _groupContainer.Controls.Clear();

        // 根据配置对存档进行分组
        var grouped = GroupSaves(_allSaves, _translateEnabled);
        foreach (var group in grouped)
        {
            // 为每个分组创建一个面板容器
            var groupPanel = new Panel
            {
                Width = ComputeGroupPanelWidth(),
                Height = 194,
                Margin = new Padding(0, 0, 0, 12),
                BackColor = Color.White,
            };
            UiPerformance.EnableDoubleBuffer(groupPanel);

            // 添加分组标题标签
            var title = new Label
            {
                Text = group.DisplayName,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(2, 2, 0, 0),
            };
            groupPanel.Controls.Add(title);

            // 添加标题下方的装饰线
            var line = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Color.FromArgb(220, 224, 232),
            };
            groupPanel.Controls.Add(line);

            // 创建用于水平排列瓷砖的流式面板
            var row = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(4, 8, 4, 4),
            };
            UiPerformance.EnableDoubleBuffer(row);
            groupPanel.Controls.Add(row);

            var index = 0;
            // 按规则（先非Meta，后Meta；再按槽位排序）遍历分组内的每个存档条目
            foreach (var entry in group.Entries.OrderBy(x => x.IsMeta).ThenBy(x => x.Slot))
            {
                // 在第四个瓷砖（index == 3）前插入一个视觉分隔符
                if (index == 3)
                {
                    row.Controls.Add(new Panel
                    {
                        Width = 2,
                        Height = 126,
                        Margin = new Padding(8, 0, 8, 0),
                        BackColor = Color.FromArgb(180, 180, 180),
                    });
                }

                // 创建并配置瓷砖控件
                var tile = new SaveTileControl(entry);
                tile.SetMultiSelect(MultiSelectMode);
                tile.SetPending(_pendingPaths.Contains(NormalizePath(entry.FullPath)));
                // 订阅瓷砖的事件
                tile.TileClicked += (_, e) => OnTileClicked(tile, e);
                tile.TileDoubleClicked += (_, e) => TileDoubleClicked?.Invoke(this, e);
                tile.MoveRequested += (_, e) => MoveRequested?.Invoke(this, e);
                _tiles.Add(tile);
                row.Controls.Add(tile);

                // 如果该条目在重建前是选中的，则在重建后恢复其选中状态
                if (selectedPaths.Contains(NormalizePath(entry.FullPath)))
                {
                    tile.SetSelected(true);
                    _selected.Add(tile);
                }

                index++;
            }

            // 将分组面板添加到主容器，并记录引用
            _groupContainer.Controls.Add(groupPanel);
            _groupPanels.Add(groupPanel);
        }

        // 恢复布局，调整宽度，并触发一次选择变更通知
        ResumeLayout();
        ReflowGroupPanelWidths();
        EmitSelectionChanged();
    }

    /// <summary>
    /// 处理瓷砖的点击事件，实现单选或多选逻辑。
    /// </summary>
    /// <param name="tile"> 被点击的瓷砖控件 </param>
    /// <param name="_"> 事件参数（未使用） </param>
    private void OnTileClicked(SaveTileControl tile, SaveFileEntry _)
    {
        var ctrl = (ModifierKeys & Keys.Control) == Keys.Control;
        // 决定是追加选择（多选）还是替换选择（单选）
        var append = MultiSelectMode || ctrl;

        if (!append)
        {
            // 单选模式：取消所有其他已选中的瓷砖
            foreach (var current in _selected.ToList())
            {
                if (current == tile)
                {
                    continue;
                }

                current.SetSelected(false);
                _selected.Remove(current);
            }

            // 如果点击的瓷砖未被选中，则选中它
            if (!_selected.Contains(tile))
            {
                tile.SetSelected(true);
                _selected.Add(tile);
            }
        }
        else
        {
            // 多选模式：切换当前瓷砖的选中状态
            if (_selected.Contains(tile))
            {
                tile.SetSelected(false);
                _selected.Remove(tile);
            }
            else
            {
                tile.SetSelected(true);
                _selected.Add(tile);
            }
        }

        EmitSelectionChanged();
    }

    /// <summary>
    /// 触发 SelectionChanged 事件。
    /// </summary>
    private void EmitSelectionChanged()
    {
        SelectionChanged?.Invoke(this, _selected.Select(x => x.Entry).ToList());
    }

    /// <summary>
    /// 根据配置对存档列表进行分组。
    /// </summary>
    /// <param name="saves"> 存档列表 </param>
    /// <param name="translated"> 是否使用翻译后的显示名称 </param>
    /// <returns> 分组后的存档集合 </returns>
    private static IReadOnlyList<GroupedSaves> GroupSaves(IReadOnlyList<SaveFileEntry> saves, bool translated)
    {
        var config = SaveDisplayConfig.Instance;
        // 使用配置中的规则进行分组、排序和格式化显示名称
        var groups = saves
            .GroupBy(config.GetGroupKey)
            .Select(x => new GroupedSaves(
                x.Key,
                config.GetGroupDisplayName(x.Key, translated),
                x.ToList()))
            .OrderBy(x => config.GetGroupOrder(x.GroupKey))
            .ThenBy(x => x.GroupKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (groups.Count == 0)
        {
            return [];
        }

        return groups;
    }

    /// <summary>
    /// 计算分组面板的宽度，确保不小于最小值且适配可用空间。
    /// </summary>
    /// <returns> 计算出的宽度像素值 </returns>
    private int ComputeGroupPanelWidth()
    {
        // 计算滚动面板的可用宽度（减去滚动条和边距）
        var available = _scrollPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 18;
        // 返回可用宽度与最小宽度（620）中的较大值
        return Math.Max(620, available);
    }

    /// <summary>
    /// 调整所有分组面板的宽度至计算出的目标宽度。
    /// </summary>
    private void ReflowGroupPanelWidths()
    {
        var targetWidth = ComputeGroupPanelWidth();
        foreach (var panel in _groupPanels)
        {
            panel.Width = targetWidth;
        }
    }

    /// <summary>
    /// 规范化文件路径，将其转换为完整路径并去除末尾的目录分隔符。
    /// </summary>
    /// <param name="path"> 原始路径字符串 </param>
    /// <returns> 规范化后的路径字符串 </returns>
    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// 内部记录，用于存储分组后的存档信息。
    /// </summary>
    /// <param name="GroupKey"> 分组的键 </param>
    /// <param name="DisplayName"> 分组的显示名称（可能已翻译） </param>
    /// <param name="Entries"> 该分组包含的存档条目列表 </param>
    private sealed record GroupedSaves(string GroupKey, string DisplayName, List<SaveFileEntry> Entries);
}
