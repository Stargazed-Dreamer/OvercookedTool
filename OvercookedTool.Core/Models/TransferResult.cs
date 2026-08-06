namespace OvercookedTool.Core.Models;

/// <summary>
/// TransferResult 类用于表示文件传输操作的结果。
/// </summary>
public sealed class TransferResult
{
    /// <summary>
    /// 获取一个值，该值指示传输操作是否成功完成。
    /// </summary>
    public bool Success { get; init; }
    /// <summary>
    /// 获取传输操作的结果消息或错误信息。如果操作成功，此属性可能为空字符串。
    /// </summary>
    public string Message { get; init; } = string.Empty;
    /// <summary>
    /// 获取传输操作的目标文件路径。此属性可能为 null，例如当操作失败或不适用时。
    /// </summary>
    public string? TargetPath { get; init; }
}

