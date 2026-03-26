using System.ComponentModel;
using OvercookedTool.Core.Models;

namespace OvercookedTool.App;

internal sealed class SaveTileMatrixView : UserControl
{
    private readonly Panel _scrollPanel;
    private readonly FlowLayoutPanel _groupContainer;
    private readonly List<Panel> _groupPanels = new();
    private readonly List<SaveTileControl> _tiles = new();
    private readonly List<SaveTileControl> _selected = new();
    private IReadOnlyList<SaveFileEntry> _allSaves = Array.Empty<SaveFileEntry>();
    private readonly HashSet<string> _pendingPaths = new(StringComparer.OrdinalIgnoreCase);
    private bool _translateEnabled = true;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool MultiSelectMode { get; private set; }

    public event EventHandler<IReadOnlyList<SaveFileEntry>>? SelectionChanged;
    public event EventHandler<SaveFileEntry>? TileDoubleClicked;
    public event EventHandler<MovePositionRequest>? MoveRequested;

    public SaveTileMatrixView()
    {
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

        _groupContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };
        UiPerformance.EnableDoubleBuffer(_groupContainer);
        _scrollPanel.Controls.Add(_groupContainer);
        Resize += (_, _) => ReflowGroupPanelWidths();
        _scrollPanel.Resize += (_, _) => ReflowGroupPanelWidths();
    }

    public IReadOnlyList<SaveFileEntry> GetSelectedEntries()
    {
        return _selected.Select(x => x.Entry).ToList();
    }

    public void SetTranslateEnabled(bool enabled)
    {
        if (_translateEnabled == enabled)
        {
            return;
        }

        _translateEnabled = enabled;
        RebuildView();
    }

    public void SetPendingSaves(IReadOnlyCollection<string> fullPaths)
    {
        _pendingPaths.Clear();
        foreach (var path in fullPaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                _pendingPaths.Add(NormalizePath(path));
            }
        }

        foreach (var tile in _tiles)
        {
            tile.SetPending(_pendingPaths.Contains(NormalizePath(tile.Entry.FullPath)));
        }
    }

    public void SetMultiSelect(bool enabled)
    {
        MultiSelectMode = enabled;
        foreach (var tile in _tiles)
        {
            tile.SetMultiSelect(enabled);
        }

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

    public void SetSaves(IReadOnlyList<SaveFileEntry> saves)
    {
        _allSaves = saves.ToList();
        RebuildView();
    }

    private void RebuildView()
    {
        var selectedPaths = _selected.Select(x => NormalizePath(x.Entry.FullPath)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        SuspendLayout();
        _tiles.Clear();
        _selected.Clear();
        _groupPanels.Clear();
        _groupContainer.Controls.Clear();

        var grouped = GroupSaves(_allSaves, _translateEnabled);
        foreach (var group in grouped)
        {
            var groupPanel = new Panel
            {
                Width = ComputeGroupPanelWidth(),
                Height = 194,
                Margin = new Padding(0, 0, 0, 12),
                BackColor = Color.White,
            };
            UiPerformance.EnableDoubleBuffer(groupPanel);

            var title = new Label
            {
                Text = group.DisplayName,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(2, 2, 0, 0),
            };
            groupPanel.Controls.Add(title);

            var line = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Color.FromArgb(220, 224, 232),
            };
            groupPanel.Controls.Add(line);

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
            foreach (var entry in group.Entries.OrderBy(x => x.IsMeta).ThenBy(x => x.Slot))
            {
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

                var tile = new SaveTileControl(entry);
                tile.SetMultiSelect(MultiSelectMode);
                tile.SetPending(_pendingPaths.Contains(NormalizePath(entry.FullPath)));
                tile.TileClicked += (_, e) => OnTileClicked(tile, e);
                tile.TileDoubleClicked += (_, e) => TileDoubleClicked?.Invoke(this, e);
                tile.MoveRequested += (_, e) => MoveRequested?.Invoke(this, e);
                _tiles.Add(tile);
                row.Controls.Add(tile);

                if (selectedPaths.Contains(NormalizePath(entry.FullPath)))
                {
                    tile.SetSelected(true);
                    _selected.Add(tile);
                }

                index++;
            }

            _groupContainer.Controls.Add(groupPanel);
            _groupPanels.Add(groupPanel);
        }

        ResumeLayout();
        ReflowGroupPanelWidths();
        EmitSelectionChanged();
    }

    private void OnTileClicked(SaveTileControl tile, SaveFileEntry _)
    {
        var ctrl = (ModifierKeys & Keys.Control) == Keys.Control;
        var append = MultiSelectMode || ctrl;

        if (!append)
        {
            foreach (var current in _selected.ToList())
            {
                if (current == tile)
                {
                    continue;
                }

                current.SetSelected(false);
                _selected.Remove(current);
            }

            if (!_selected.Contains(tile))
            {
                tile.SetSelected(true);
                _selected.Add(tile);
            }
        }
        else
        {
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

    private void EmitSelectionChanged()
    {
        SelectionChanged?.Invoke(this, _selected.Select(x => x.Entry).ToList());
    }

    private static IReadOnlyList<GroupedSaves> GroupSaves(IReadOnlyList<SaveFileEntry> saves, bool translated)
    {
        var config = SaveDisplayConfig.Instance;
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

    private int ComputeGroupPanelWidth()
    {
        var available = _scrollPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 18;
        return Math.Max(620, available);
    }

    private void ReflowGroupPanelWidths()
    {
        var targetWidth = ComputeGroupPanelWidth();
        foreach (var panel in _groupPanels)
        {
            panel.Width = targetWidth;
        }
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private sealed record GroupedSaves(string GroupKey, string DisplayName, List<SaveFileEntry> Entries);
}
