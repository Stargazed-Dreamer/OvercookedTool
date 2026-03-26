using OvercookedTool.Core.Models;

namespace OvercookedTool.App;

internal sealed record TransferRequest(IReadOnlyList<SaveFileEntry> Saves, bool Move);

internal sealed record MovePositionRequest(SaveFileEntry Save, string Direction);

