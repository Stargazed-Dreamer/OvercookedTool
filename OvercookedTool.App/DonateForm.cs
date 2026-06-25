namespace OvercookedTool.App;

/// <summary>
/// 打赏窗体类，用于显示打赏信息和图片
/// </summary>
internal sealed class DonateForm : Form
{
    /// <summary>
    /// 初始化打赏窗体，设置窗体属性、布局和内容
    /// </summary>
    public DonateForm()
    {
        // 设置窗体基本属性
        Text = "打赏";
        Width = 520;
        Height = 520;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        // 创建主表格布局面板
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12),
        };
        // 设置三行布局：自适应高度、填充剩余空间、自适应高度
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        // 添加顶部文本标签
        root.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "哇沃，我喜欢你╰(*°▽°*)╯",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 4, 0, 8),
        }, 0, 0);

        // 创建图片显示面板
        var picturePanel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
        };
        root.Controls.Add(picturePanel, 0, 1);

        // 尝试加载图片文件
        var imagePath = Path.Combine(AppContext.BaseDirectory, "libcoffee.dll");
        if (File.Exists(imagePath))
        {
            try
            {
                // 读取图片文件并创建位图副本
                using var fs = File.OpenRead(imagePath);
                using var img = Image.FromStream(fs);
                var copy = new Bitmap(img);
                // 创建图片显示控件
                picturePanel.Controls.Add(new PictureBox
                {
                    Dock = DockStyle.Fill,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = copy,
                });
            }
            catch
            {
                // 图片解析失败时显示错误信息
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
            // 图片文件不存在时显示提示信息
            picturePanel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "图片未找到",
            });
        }

        // 创建底部按钮区域
        var close = new Button
        {
            Text = "关闭",
            AutoSize = true,
        };
        // 绑定关闭按钮点击事件
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
