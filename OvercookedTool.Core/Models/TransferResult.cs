namespace OvercookedTool.Core.Models;

public sealed class TransferResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? TargetPath { get; init; }
}

