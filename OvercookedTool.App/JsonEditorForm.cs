using OvercookedTool.Core.Models;

namespace OvercookedTool.App;

/// <summary>
/// JSON编辑器窗体，提供一个基于文本框的JSON内容编辑环境。
/// 继承自System.Windows.Forms.Form，用于显示和编辑JSON文本数据。
/// </summary>
internal sealed class JsonEditorForm : Form
{
    /// <summary>
    /// 用于显示和编辑JSON文本的文本框控件实例。
    /// </summary>
    private readonly TextBox _editor;

    /// <summary>
    /// 获取编辑器中的当前JSON文本内容。
    /// </summary>
    public string JsonText => _editor.Text;

    /// <summary>
    /// JsonEditorForm构造函数，初始化窗体及其包含的UI控件。
    /// </summary>
    /// <param name="fileName">要编辑的JSON文件名，用于在标题栏显示。</param>
    /// <param name="version">检测到的存档版本信息，用于在标题栏显示。</param>
    /// <param name="jsonText">需要显示和编辑的JSON文本内容。</param>
    public JsonEditorForm(string fileName, SaveVersion version, string jsonText)
    {
        // 设置窗体标题，显示正在编辑的文件名。
        Text = $"编辑存档 JSON - {fileName}";
        // 设置窗体默认宽度。
        Width = 960;
        // 设置窗体默认高度。
        Height = 720;
        // 设置窗体启动位置为父窗体中央。
        StartPosition = FormStartPosition.CenterParent;

        // 创建根表格布局面板，用于组织窗体内的控件。
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
        };
        // 为表格布局添加行样式：第一行自适应大小（用于标题）。
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // 第二行填充剩余空间（用于编辑器）。
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        // 第三行自适应大小（用于按钮面板）。
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        // 创建标题标签，显示文件名和版本信息。
        var header = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10),
            Text = $"文件: {fileName} | 检测版本: {version}",
        };
        // 将标题标签放置到表格的第一行。
        root.Controls.Add(header, 0, 0);

        // 创建多行文本编辑器，用于编辑JSON。
        _editor = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            // 显示水平和垂直滚动条。
            ScrollBars = ScrollBars.Both,
            // 使用等宽字体便于JSON格式查看。
            Font = new Font("Consolas", 10),
            // 关闭自动换行以保持原始格式。
            WordWrap = false,
            // 允许输入回车键（换行）。
            AcceptsReturn = true,
            // 允许输入制表键（用于缩进）。
            AcceptsTab = true,
            // 加载传入的JSON文本。
            Text = jsonText,
        };
        // 将文本编辑器放置到表格的第二行。
        root.Controls.Add(_editor, 0, 1);

        // 创建按钮面板，使用流式布局并右对齐按钮。
        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            AutoSize = true,
        };

        // 创建“保存”按钮。
        var saveButton = new Button
        {
            Text = "保存",
            AutoSize = true,
        };
        // 为“保存”按钮绑定点击事件：将对话框结果设为OK并关闭窗体。
        saveButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };

        // 创建“取消”按钮。
        var cancelButton = new Button
        {
            Text = "取消",
            AutoSize = true,
        };
        // 为“取消”按钮绑定点击事件：将对话框结果设为Cancel并关闭窗体。
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        // 将“保存”和“取消”按钮添加到按钮面板。
        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(cancelButton);
        // 将按钮面板放置到表格的第三行。
        root.Controls.Add(buttonPanel, 0, 2);
    }
}

