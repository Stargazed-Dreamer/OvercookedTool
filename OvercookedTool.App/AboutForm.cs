namespace OvercookedTool.App;

internal sealed class AboutForm : Form
{
    public AboutForm()
    {
        var content = AboutContentProvider.Load();

        Text = "关于";
        Width = 620;
        Height = 430;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(246, 250, 255);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16),
            BackColor = BackColor,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

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

        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            BackColor = Color.White,
            Padding = new Padding(14),
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        AddTextRow(card, 0, "版本:", content.Version);
        AddTextRow(card, 1, "Q群:", content.QqGroup);
        AddLinkRow(card, 2, "GitHub:", content.GithubUrl);
        AddLinkRow(card, 3, "Bilibili:", content.BilibiliUrl);

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

        var close = new Button
        {
            Text = "关闭",
            AutoSize = true,
        };
        close.Click += (_, _) => Close();
        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
        };
        buttonRow.Controls.Add(close);
        root.Controls.Add(buttonRow, 0, 2);
    }

    private static void AddTextRow(TableLayoutPanel parent, int row, string label, string value)
    {
        parent.Controls.Add(new Label
        {
            AutoSize = true,
            Text = label,
            Margin = new Padding(0, 6, 8, 2),
        }, 0, row);
        parent.Controls.Add(new Label
        {
            AutoSize = true,
            Text = value,
            Margin = new Padding(0, 6, 0, 2),
        }, 1, row);
    }

    private static void AddLinkRow(TableLayoutPanel parent, int row, string label, string url)
    {
        parent.Controls.Add(new Label
        {
            AutoSize = true,
            Text = label,
            Margin = new Padding(0, 6, 8, 2),
        }, 0, row);

        var safeUrl = string.IsNullOrWhiteSpace(url) ? "-" : url;
        var link = new LinkLabel
        {
            AutoSize = true,
            Text = safeUrl,
            LinkColor = Color.FromArgb(25, 118, 210),
            Margin = new Padding(0, 6, 0, 2),
        };
        link.LinkClicked += (_, _) =>
        {
            if (safeUrl != "-")
            {
                System.Diagnostics.Process.Start("explorer.exe", safeUrl);
            }
        };
        parent.Controls.Add(link, 1, row);
    }
}
