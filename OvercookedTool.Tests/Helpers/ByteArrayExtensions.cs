namespace OvercookedTool.Tests.Helpers;

/// <summary>
/// 字节数组辅助扩展方法。
/// </summary>
internal static class ByteArrayExtensions
{
    /// <summary>
    /// 去除字节数组末尾所有等于指定值的字节（类似 string.TrimEnd）。
    /// </summary>
    public static byte[] TrimEnd(this byte[] data, byte value)
    {
        var end = data.Length;
        while (end > 0 && data[end - 1] == value) end--;
        return end == data.Length ? data : data[..end];
    }
}
