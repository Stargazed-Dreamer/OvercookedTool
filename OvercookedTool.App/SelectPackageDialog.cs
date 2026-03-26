using System.ComponentModel;

namespace OvercookedTool.App;

internal sealed class SelectPackageDialog : Form
{
    private readonly ListBox _list;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? SelectedPath { get; private set; }

    public SelectPackageDialog(string title, string description, IReadOnlyList<string> candidates)
    {
        Text = title;
        Width = 680;
        Height = 420;
        StartPosition = FormStartPosition.CenterParent;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(new Label { Text = description, AutoSize = true }, 0, 0);

        _list = new ListBox { Dock = DockStyle.Fill };
        _list.Items.AddRange(candidates.Cast<object>().ToArray());
        _list.DoubleClick += (_, _) => Confirm();
        root.Controls.Add(_list, 0, 1);

        var actions = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
        };
        var ok = new Button { Text = "确定", AutoSize = true };
        ok.Click += (_, _) => Confirm();
        var cancel = new Button { Text = "取消", AutoSize = true };
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        actions.Controls.Add(ok);
        actions.Controls.Add(cancel);
        root.Controls.Add(actions, 0, 2);
    }

    private void Confirm()
    {
        if (_list.SelectedItem is not string path)
        {
            MessageBox.Show("请先选择一个目标。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SelectedPath = path;
        DialogResult = DialogResult.OK;
        Close();
    }
}
