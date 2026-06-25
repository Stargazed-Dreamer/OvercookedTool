using System.Diagnostics;

namespace OvercookedTool.App;

/// <summary>
/// Unity设备标识对话框，用于让用户输入或粘贴Unity引擎生成的设备唯一标识。
/// 该标识用于胡闹厨房游戏离线模式下的存档加密。
/// </summary>
internal sealed class UnityDeviceIdDialog : Form
{
    /// <summary>
    /// 设备标识输入框
    /// </summary>
    private TextBox _inputBox = null!;

    /// <summary>
    /// 确认按钮
    /// </summary>
    private Button _okBtn = null!;

    /// <summary>
    /// 用户输入的设备标识，对话框关闭后可通过此属性获取
    /// </summary>
    public string? EnteredDeviceId { get; private set; }

    /// <summary>
    /// 初始化Unity设备标识对话框
    /// </summary>
    /// <param name="existingId">已存在的设备标识，用于预填充输入框</param>
    public UnityDeviceIdDialog(string? existingId = null)
    {
        Text = "本机 Unity 设备标识";
        ClientSize = new Size(520, 440);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(244, 248, 255);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        // 初始化对话框内的所有UI组件
        InitializeComponents(existingId);
    }

    /// <summary>
    /// 初始化对话框内的所有UI组件
    /// </summary>
    /// <param name="existingId">已存在的设备标识，用于预填充输入框</param>
    private void InitializeComponents(string? existingId)
    {
        // 创建标题标签
        var title = new Label
        {
            Text = "本机 Unity 设备标识",
            Location = new Point(0, 16),
            Size = new Size(520, 36),
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Color.FromArgb(13, 71, 161),
            TextAlign = ContentAlignment.MiddleCenter,
        };
        Controls.Add(title);

        // 创建输入框标签
        var inputLabel = new Label
        {
            Text = "设备标识:",
            Location = new Point(24, 66),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 33, 33),
        };
        Controls.Add(inputLabel);

        // 创建设备标识输入框，如果存在已有标识则预填充
        _inputBox = new TextBox
        {
            Text = existingId ?? string.Empty,
            Location = new Point(24, 88),
            Size = new Size(472, 28),
            Font = new Font("Consolas", 11),
            BackColor = Color.FromArgb(248, 250, 255),
            ForeColor = Color.FromArgb(13, 71, 161),
            PlaceholderText = "在此粘贴 Unity 设备标识",
        };
        // 输入框内容变化时更新确认按钮状态
        _inputBox.TextChanged += (_, _) => UpdateOkButton();
        Controls.Add(_inputBox);

        // 创建打开Unity工具按钮
        var openHarnessBtn = new Button
        {
            Text = "打开 Unity 工具",
            Location = new Point(24, 128),
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(255, 224, 130),
            ForeColor = Color.FromArgb(100, 70, 0),
            Font = new Font("Segoe UI", 10),
            Padding = new Padding(16, 4, 16, 4),
            FlatAppearance = { BorderColor = Color.FromArgb(220, 190, 80) },
        };
        // 点击按钮时打开Unity设备标识工具
        openHarnessBtn.Click += (_, _) => OpenHarness();
        Controls.Add(openHarnessBtn);

        // 创建说明面板
        var descPanel = new Panel
        {
            Location = new Point(12, 172),
            Size = new Size(496, 180),
            BackColor = Color.FromArgb(255, 248, 225),
        };
        Controls.Add(descPanel);

        // 创建说明文本，解释设备标识的用途和使用方法
        var desc = new Label
        {
            Text = "胡闹厨房在离线模式下使用 Unity 引擎生成的设备唯一标识作为存档加密密码。\n" +
                   "请点击上方【打开 Unity 工具】按钮，在弹出的窗口中点击 Copy 复制标识，" +
                   "然后粘贴到上方输入框中。此标识仅需设置一次。",
            Location = new Point(12, 10),
            Size = new Size(472, 160),
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(80, 60, 0),
        };
        descPanel.Controls.Add(desc);

        // 创建确认按钮，初始状态根据是否已有标识决定是否启用
        _okBtn = new Button
        {
            Text = "确定",
            Location = new Point(404, 390),
            Size = new Size(92, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(227, 242, 253),
            ForeColor = Color.FromArgb(13, 71, 161),
            Font = new Font("Segoe UI", 10),
            FlatAppearance = { BorderColor = Color.FromArgb(144, 202, 249) },
            // 如果已有标识则启用按钮，否则禁用
            Enabled = !string.IsNullOrWhiteSpace(existingId),
        };
        // 点击确认按钮时保存输入的设备标识并关闭对话框
        _okBtn.Click += (_, _) =>
        {
            EnteredDeviceId = _inputBox.Text.Trim();
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(_okBtn);
    }

    /// <summary>
    /// 根据输入框内容更新确认按钮的启用状态
    /// </summary>
    private void UpdateOkButton()
    {
        // 仅当输入框有非空白内容时启用确认按钮
        _okBtn.Enabled = !string.IsNullOrWhiteSpace(_inputBox.Text.Trim());
    }

    /// <summary>
    /// 打开Unity设备标识工具程序
    /// </summary>
    private void OpenHarness()
    {
        // 拼接Unity工具程序的完整路径
        var exePath = Path.Combine(AppContext.BaseDirectory, "UnityHarness", "_UnityDeviceUniqueIdentifierHarness.exe");
        // 检查工具程序是否存在
        if (!File.Exists(exePath))
        {
            MessageBox.Show("Unity 设备标识工具未找到，请确认 UnityHarness 目录完整。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            // 启动Unity设备标识工具进程
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            // 启动失败时显示错误信息
            MessageBox.Show($"启动失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
