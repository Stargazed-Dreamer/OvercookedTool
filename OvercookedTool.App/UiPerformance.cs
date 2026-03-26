using System.Reflection;

namespace OvercookedTool.App;

internal static class UiPerformance
{
    private static readonly PropertyInfo? DoubleBufferedProperty =
        typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void EnableDoubleBuffer(Control control)
    {
        if (SystemInformation.TerminalServerSession)
        {
            return;
        }

        DoubleBufferedProperty?.SetValue(control, true);
    }
}
