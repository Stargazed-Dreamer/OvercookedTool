using OvercookedTool.Core.Models;

namespace OvercookedTool.App;

internal sealed class JsonEditorForm : Form
{
    private readonly TextBox _editor;

    public string JsonText => _editor.Text;

    public JsonEditorForm(string fileName, SaveVersion version, string jsonText)
    {
        Text = $"编辑存档 JSON - {fileName}";
        Width = 960;
        Height = 720;
        StartPosition = FormStartPosition.CenterParent;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var header = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10),
            Text = $"文件: {fileName} | 检测版本: {version}",
        };
        root.Controls.Add(header, 0, 0);

        _editor = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 10),
            WordWrap = false,
            AcceptsReturn = true,
            AcceptsTab = true,
            Text = jsonText,
        };
        root.Controls.Add(_editor, 0, 1);

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            AutoSize = true,
        };

        var saveButton = new Button
        {
            Text = "保存",
            AutoSize = true,
        };
        saveButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };

        var cancelButton = new Button
        {
            Text = "取消",
            AutoSize = true,
        };
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(cancelButton);
        root.Controls.Add(buttonPanel, 0, 2);
    }
}

