using System.Reflection;

namespace OvercookedTool.App;

/// <summary>
/// 用于提高用户界面性能的工具类，通过启用控件的双缓冲来减少绘制时的闪烁。
/// </summary>
internal static class UiPerformance
{
    // 使用反射获取Control类的DoubleBuffered属性，该属性是私有的，用于控制双缓冲。
    private static readonly PropertyInfo? DoubleBufferedProperty =
        typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);

    // 启用指定控件的双缓冲，如果当前是终端服务器会话则跳过。
    public static void EnableDoubleBuffer(Control control)
    {
        // 如果在终端服务器会话中，双缓冲可能无效，因此直接返回。
        if (SystemInformation.TerminalServerSession)
        {
            return;
        }

        // 使用反射设置控件的DoubleBuffered属性为true，启用双缓冲。
        DoubleBufferedProperty?.SetValue(control, true);
    }
}
