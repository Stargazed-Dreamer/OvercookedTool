namespace OvercookedTool.App;

internal sealed class DonateForm : Form
{
    public DonateForm()
    {
        Text = "打赏";
        Width = 520;
        Height = 520;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "哇沃，我喜欢你╰(*°▽°*)╯",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 4, 0, 8),
        }, 0, 0);

        var picturePanel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
        };
        root.Controls.Add(picturePanel, 0, 1);

        var imagePath = Path.Combine(AppContext.BaseDirectory, "libcoffee.dll");
        if (File.Exists(imagePath))
        {
            try
            {
                using var fs = File.OpenRead(imagePath);
                using var img = Image.FromStream(fs);
                var copy = new Bitmap(img);
                picturePanel.Controls.Add(new PictureBox
                {
                    Dock = DockStyle.Fill,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = copy,
                });
            }
            catch
            {
                picturePanel.Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = "图片解析失败",
                });
            }
        }
        else
        {
            picturePanel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "图片未找到",
            });
        }

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
}
