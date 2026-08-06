namespace OvercookedTool.App;

/// <summary>
/// 表示“关于”窗口的窗体类，用于展示应用程序的版本、作者、链接等信息。
/// </summary>
internal sealed class AboutForm : Form
{
    public AboutForm()
    {
        // 从内容提供器加载关于信息
        var content = AboutContentProvider.Load();

        // 设置窗口基本属性
        Text = "关于";
        Width = 620;
        Height = 430;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(246, 250, 255);

        // 创建根布局容器，使用表格布局管理子控件
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16),
            BackColor = BackColor,
        };
        // 设置行样式：第一行和最后一行自动调整大小，中间行填充剩余空间
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        // 添加标题标签，显示应用程序名称
        var title = new Label
        {
            Text = "胡闹厨房存档管理器",
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Top,
            ForeColor = Color.FromArgb(13, 71, 161),
            Margin = new Padding(0, 0, 0, 8),
        };
        root.Controls.Add(title, 0, 0);

        // 创建卡片布局容器，用于组织详细信息
        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            BackColor = Color.White,
            Padding = new Padding(14),
        };
        // 设置卡片列样式：第一列自动调整大小，第二列填充剩余空间
        card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // 设置卡片行样式：前四行自动调整大小，最后一行填充剩余空间
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // 添加文本行显示版本和Q群信息
        AddTextRow(card, 0, "版本:", content.Version);
        AddTextRow(card, 1, "Q群:", content.QqGroup);
        // 添加链接行显示GitHub和Bilibili链接
        AddLinkRow(card, 2, "GitHub:", content.GithubUrl);
        AddLinkRow(card, 3, "Bilibili:", content.BilibiliUrl);

        // 添加作者标签，跨越两列显示
        var author = new Label
        {
            AutoSize = true,
            Text = "作者：" + content.Author,
            ForeColor = Color.FromArgb(97, 97, 97),
            Margin = new Padding(0, 12, 0, 0),
        };
        card.Controls.Add(author, 0, 4);
        card.SetColumnSpan(author, 2);

        root.Controls.Add(card, 0, 1);

        // 创建关闭按钮，并绑定点击事件以关闭窗口
        var close = new Button
        {
            Text = "关闭",
            AutoSize = true,
        };
        close.Click += (_, _) => Close();
        // 创建按钮行布局，使用右对齐流布局
        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
        };
        buttonRow.Controls.Add(close);
        root.Controls.Add(buttonRow, 0, 2);
    }

    /// <summary>
    /// 向表格布局中添加一行文本信息，包括标签和值。
    /// </summary>
    /// <param name="parent">父表格布局容器。</param>
    /// <param name="row">行索引。</param>
    /// <param name="label">标签文本。</param>
    /// <param name="value">值文本。</param>
    private static void AddTextRow(TableLayoutPanel parent, int row, string label, string value)
    {
        // 添加标签控件到指定行的第一列
        parent.Controls.Add(new Label
        {
            AutoSize = true,
            Text = label,
            Margin = new Padding(0, 6, 8, 2),
        }, 0, row);
        // 添加值控件到指定行的第二列
        parent.Controls.Add(new Label
        {
            AutoSize = true,
            Text = value,
            Margin = new Padding(0, 6, 0, 2),
        }, 1, row);
    }

    /// <summary>
    /// 向表格布局中添加一行链接信息，包括标签和可点击的链接。
    /// </summary>
    /// <param name="parent">父表格布局容器。</param>
    /// <param name="row">行索引。</param>
    /// <param name="label">标签文本。</param>
    /// <param name="url">链接URL地址。</param>
    private static void AddLinkRow(TableLayoutPanel parent, int row, string label, string url)
    {
        // 添加标签控件到指定行的第一列
        parent.Controls.Add(new Label
        {
            AutoSize = true,
            Text = label,
            Margin = new Padding(0, 6, 8, 2),
        }, 0, row);

        // 处理空URL情况，使用占位符
        var safeUrl = string.IsNullOrWhiteSpace(url) ? "-" : url;
        // 创建链接标签控件
        var link = new LinkLabel
        {
            AutoSize = true,
            Text = safeUrl,
            LinkColor = Color.FromArgb(25, 118, 210),
            Margin = new Padding(0, 6, 0, 2),
        };
        // 绑定链接点击事件，使用系统默认浏览器打开URL
        link.LinkClicked += (_, _) =>
        {
            if (safeUrl != "-")
            {
                System.Diagnostics.Process.Start("explorer.exe", safeUrl);
            }
        };
        // 添加链接控件到指定行的第二列
        parent.Controls.Add(link, 1, row);
    }
}
