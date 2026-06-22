using System.Diagnostics;

namespace OvercookedTool.App;

internal sealed class UnityDeviceIdDialog : Form
{
    private TextBox _inputBox = null!;
    private Button _okBtn = null!;

    public string? EnteredDeviceId { get; private set; }

    public UnityDeviceIdDialog(string? existingId = null)
    {
        Text = "本机 Unity 设备标识";
        ClientSize = new Size(520, 440);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(244, 248, 255);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        InitializeComponents(existingId);
    }

    private void InitializeComponents(string? existingId)
    {
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

        var inputLabel = new Label
        {
            Text = "设备标识:",
            Location = new Point(24, 66),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 33, 33),
        };
        Controls.Add(inputLabel);

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
        _inputBox.TextChanged += (_, _) => UpdateOkButton();
        Controls.Add(_inputBox);

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
        openHarnessBtn.Click += (_, _) => OpenHarness();
        Controls.Add(openHarnessBtn);

        var descPanel = new Panel
        {
            Location = new Point(12, 172),
            Size = new Size(496, 180),
            BackColor = Color.FromArgb(255, 248, 225),
        };
        Controls.Add(descPanel);

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
            Enabled = !string.IsNullOrWhiteSpace(existingId),
        };
        _okBtn.Click += (_, _) =>
        {
            EnteredDeviceId = _inputBox.Text.Trim();
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(_okBtn);
    }

    private void UpdateOkButton()
    {
        _okBtn.Enabled = !string.IsNullOrWhiteSpace(_inputBox.Text.Trim());
    }

    private void OpenHarness()
    {
        var exePath = Path.Combine(AppContext.BaseDirectory, "UnityHarness", "_UnityDeviceUniqueIdentifierHarness.exe");
        if (!File.Exists(exePath))
        {
            MessageBox.Show("Unity 设备标识工具未找到，请确认 UnityHarness 目录完整。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"启动失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
