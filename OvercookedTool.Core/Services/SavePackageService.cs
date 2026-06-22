using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using OvercookedTool.Core.Crypto;
using OvercookedTool.Core.Logging;
using OvercookedTool.Core.Models;

namespace OvercookedTool.Core.Services;

public sealed class SavePackageService
{
    private const int DefaultBackupHistoryPerSave = 10;
    private readonly KeyDetector _keyDetector = new();
    public int BackupHistoryPerSave { get; set; } = DefaultBackupHistoryPerSave;

    public SavePackageContext LoadPackage(string packagePath, string? preferredKey = null, bool allowEmpty = false, string? unityDeviceId = null)
    {
        if (!Directory.Exists(packagePath))
        {
            throw new DirectoryNotFoundException($"目录不存在: {packagePath}");
        }

        var files = Directory.GetFiles(packagePath, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(x => x is not null)
            .Cast<string>()
            .Where(IsRecognizedSaveFileName)
            .ToList();

        if (!allowEmpty && files.Count == 0)
        {
            throw new InvalidOperationException("目录中没有可识别存档文件");
        }

        var entries = new List<SaveFileEntry>();
        foreach (var fileName in files)
        {
            var fullPath = Path.Combine(packagePath, fileName);
            if (SaveFileNameHelper.TryParse(fileName, fullPath, out var entry))
            {
                entries.Add(entry);
            }
        }

        var platform = DetectPlatform(entries);
        var keyResult = _keyDetector.DetectKey(packagePath, platform, entries, preferredKey, unityDeviceId);
        var version = DetectPackageVersion(platform, entries, keyResult.Key);
        var friendCode = _keyDetector.TryExtractFriendCode(packagePath);
        var enriched = PopulateStarCounts(entries, platform, keyResult.Key);

        AppLogger.Info(
            $"Loaded package: path={packagePath}, platform={platform}, version={version}, keySource={keyResult.Source}, keyValid={keyResult.Success}");

        return new SavePackageContext
        {
            PackagePath = packagePath,
            DisplayName = Path.GetFileName(Path.GetFullPath(packagePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            Platform = platform,
            Version = version,
            DetectedKey = keyResult.Key,
            KeySource = keyResult.Source,
            KeyValidated = keyResult.Success || platform is SavePlatform.AyceJson or SavePlatform.SwitchJson,
            FriendCode = friendCode,
            Saves = enriched.OrderBy(x => x.IsMeta).ThenBy(x => x.DlcId ?? 0).ThenBy(x => x.Slot).ThenBy(x => x.FileName).ToList(),
        };
    }

    public bool TryResolvePackagePath(string selectedPath, out string resolvedPath, out IReadOnlyList<string> candidates)
    {
        resolvedPath = string.Empty;
        candidates = Array.Empty<string>();

        if (!Directory.Exists(selectedPath))
        {
            return false;
        }

        if (IsSavePackageDirectory(selectedPath))
        {
            resolvedPath = selectedPath;
            return true;
        }

        var found = FindCandidatePackages(selectedPath, maxDepth: 3, maxCount: 120).ToList();
        candidates = found;
        if (found.Count == 1)
        {
            resolvedPath = found[0];
            return true;
        }

        return false;
    }

    public IReadOnlyList<string> FindCandidatePackages(string rootDirectory, int maxDepth = 3, int maxCount = 120)
    {
        var result = new List<string>();
        if (!Directory.Exists(rootDirectory))
        {
            return result;
        }

        var queue = new Queue<(string Path, int Depth)>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        queue.Enqueue((rootDirectory, 0));

        while (queue.Count > 0 && result.Count < maxCount)
        {
            var (path, depth) = queue.Dequeue();
            if (!visited.Add(path))
            {
                continue;
            }

            try
            {
                if (IsSavePackageDirectory(path))
                {
                    result.Add(path);
                    continue;
                }

                if (depth >= maxDepth)
                {
                    continue;
                }

                foreach (var dir in Directory.GetDirectories(path))
                {
                    queue.Enqueue((dir, depth + 1));
                }
            }
            catch
            {
                // ignore unreadable directories
            }
        }

        return result;
    }

    public string ReadSaveAsJson(SavePackageContext package, SaveFileEntry save)
    {
        if (package.Platform is SavePlatform.AyceJson or SavePlatform.SwitchJson)
        {
            return File.ReadAllText(save.FullPath, Encoding.UTF8).TrimEnd('\0');
        }

        if (string.IsNullOrWhiteSpace(package.DetectedKey))
        {
            throw new InvalidOperationException("当前存档包缺少有效密钥，无法读取二进制存档。");
        }

        var bytes = File.ReadAllBytes(save.FullPath);
        if (!OvercookedCrypto.TryDecryptToJsonText(bytes, package.DetectedKey, out var jsonText))
        {
            throw new InvalidOperationException($"解密失败: {save.FileName}");
        }

        return jsonText;
    }

    public void WriteJsonToSave(SavePackageContext package, SaveFileEntry save, string jsonText, string backupReason = "edit")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(save.FullPath)!);
        BackupIfExists(save.FullPath, backupReason);

        if (package.Platform is SavePlatform.AyceJson or SavePlatform.SwitchJson)
        {
            File.WriteAllText(save.FullPath, jsonText, new UTF8Encoding(false));
            return;
        }

        if (string.IsNullOrWhiteSpace(package.DetectedKey))
        {
            throw new InvalidOperationException("当前存档包缺少有效密钥，无法写入二进制存档。");
        }

        var payload = Encoding.UTF8.GetBytes(jsonText);
        var encrypted = OvercookedCrypto.EncryptData(payload, package.DetectedKey)
                        ?? throw new InvalidOperationException("加密失败。");
        File.WriteAllBytes(save.FullPath, encrypted);
    }

    public TransferResult TransferSave(
        SavePackageContext sourcePackage,
        SaveFileEntry sourceSave,
        string targetDirectory,
        bool move)
    {
        try
        {
            if (!Directory.Exists(targetDirectory))
            {
                throw new DirectoryNotFoundException($"目标目录不存在: {targetDirectory}");
            }

            var targetPackage = LoadPackage(targetDirectory, allowEmpty: true);
            var targetPlatform = targetPackage.Platform == SavePlatform.Unknown ? SavePlatform.Oc2Binary : targetPackage.Platform;
            var targetVersion = targetPlatform == SavePlatform.AyceJson
                ? SaveVersion.Ayce
                : (targetPackage.Version == SaveVersion.Unknown ? SaveVersion.Oc2 : targetPackage.Version);

            var sourceJson = ReadSaveAsJson(sourcePackage, sourceSave);
            var sourceVersion = SaveJsonConverter.DetectVersion(sourceJson);
            if (sourceVersion == SaveVersion.Unknown)
            {
                sourceVersion = sourcePackage.Version == SaveVersion.Unknown ? SaveVersion.Oc2 : sourcePackage.Version;
            }

            var convertedJson = SaveJsonConverter.Convert(sourceJson, sourceVersion, targetVersion);

            var candidateName = SaveFileNameHelper.BuildFileName(targetPlatform, sourceSave);
            var targetPath = Path.Combine(targetDirectory, candidateName);
            if (PathsEqual(sourcePackage.PackagePath, targetDirectory) && PathsEqual(sourceSave.FullPath, targetPath))
            {
                var nextSlot = ComputeNextSlot(sourceSave, targetPackage.Saves);
                candidateName = SaveFileNameHelper.BuildFileName(targetPlatform, SaveFileNameHelper.WithSlot(sourceSave, nextSlot));
                targetPath = Path.Combine(targetDirectory, candidateName);
            }

            BackupIfExists(targetPath, move ? "move-overwrite-target" : "copy-overwrite-target");
            WriteConvertedPayload(targetPackage, targetPlatform, targetPath, convertedJson);
            SyncBackupHistoryForTransfer(sourceSave, targetPath, move);

            if (move)
            {
                BackupIfExists(sourceSave.FullPath, "move-delete-source");
                File.Delete(sourceSave.FullPath);
            }

            AppLogger.Info($"Transfer completed: {sourceSave.FullPath} -> {targetPath}; move={move}");
            return new TransferResult
            {
                Success = true,
                Message = move ? "移动并转换成功" : "复制并转换成功",
                TargetPath = targetPath,
            };
        }
        catch (Exception ex)
        {
            AppLogger.Error("Transfer failed.", ex);
            return new TransferResult
            {
                Success = false,
                Message = ex.Message,
            };
        }
    }

    public TransferResult DeleteSaves(SavePackageContext package, IReadOnlyList<SaveFileEntry> saves)
    {
        try
        {
            if (saves.Count == 0)
            {
                return new TransferResult { Success = false, Message = "没有选中的存档。" };
            }

            var count = 0;
            foreach (var save in saves)
            {
                if (!File.Exists(save.FullPath))
                {
                    continue;
                }

                BackupIfExists(save.FullPath, "delete");
                File.Delete(save.FullPath);
                count++;
            }

            AppLogger.Info($"Deleted saves from package={package.PackagePath}, count={count}");
            return new TransferResult
            {
                Success = true,
                Message = $"已删除 {count} 个存档。",
            };
        }
        catch (Exception ex)
        {
            AppLogger.Error("Delete saves failed.", ex);
            return new TransferResult { Success = false, Message = ex.Message };
        }
    }

    public IReadOnlyList<SaveSyncIssue> AnalyzeSyncIssues(SavePackageContext package)
    {
        var issues = new List<SaveSyncIssue>();
        foreach (var save in package.Saves.Where(x => !x.IsMeta))
        {
            if (!TryGetLatestBackup(save, out var backupPath, out var backupTime))
            {
                issues.Add(new SaveSyncIssue
                {
                    Type = SaveSyncIssueType.MissingBackup,
                    Save = save,
                    BackupPath = null,
                    BackupTime = null,
                    Message = $"{save.FileName}: 未发现工具备份（尚未触发过移动/删除/编辑等自动备份动作）",
                });
                continue;
            }

            var sourceTime = save.LastWriteTime;
            if (sourceTime > backupTime.AddSeconds(1))
            {
                issues.Add(new SaveSyncIssue
                {
                    Type = SaveSyncIssueType.PendingSyncToBackup,
                    Save = save,
                    BackupPath = backupPath,
                    BackupTime = backupTime,
                    Message = $"{save.FileName}: 源文件较新，备份待同步 ({sourceTime:MM-dd HH:mm:ss})",
                });
            }
        }

        var duplicates = package.Saves
            .Where(x => !x.IsMeta)
            .GroupBy(x => (Group: GetGroupToken(x), x.Slot))
            .Where(g => g.Count() > 1);
        foreach (var duplicate in duplicates)
        {
            foreach (var save in duplicate)
            {
                issues.Add(new SaveSyncIssue
                {
                    Type = SaveSyncIssueType.Conflict,
                    Save = save,
                    BackupPath = null,
                    BackupTime = null,
                    Message = $"{save.FileName}: 与同分组档位编号冲突 (Group={GetGroupToken(save)}, Slot={save.Slot})",
                });
            }
        }

        return issues
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Save.DlcId ?? 0)
            .ThenBy(x => x.Save.Slot)
            .ThenBy(x => x.Save.FileName)
            .ToList();
    }

    public TransferResult SyncSavesToSource(SavePackageContext package, IReadOnlyList<SaveFileEntry> saves)
    {
        if (saves.Count == 0)
        {
            return new TransferResult { Success = false, Message = "没有选中的存档。" };
        }

        var ok = 0;
        var fail = 0;
        var details = new List<string>();

        foreach (var save in saves.Where(x => !x.IsMeta))
        {
            try
            {
                if (!TryGetLatestBackup(save, out var backupPath, out var backupTime))
                {
                    BackupIfExists(save.FullPath, "sync-create");
                    ok++;
                    continue;
                }

                if (backupTime > save.LastWriteTime.AddSeconds(1))
                {
                    BackupIfExists(save.FullPath, "sync-restore");
                    File.Copy(backupPath, save.FullPath, overwrite: true);
                    ok++;
                    continue;
                }

                BackupIfExists(save.FullPath, "sync-refresh");
                ok++;
            }
            catch (Exception ex)
            {
                fail++;
                details.Add($"{save.FileName}: {ex.Message}");
            }
        }

        return new TransferResult
        {
            Success = fail == 0,
            Message = fail == 0
                ? $"同步完成，共处理 {ok} 个存档。"
                : $"同步完成，成功 {ok}，失败 {fail}。{string.Join(" | ", details.Take(3))}",
        };
    }

    public TransferResult BackupSaves(IReadOnlyList<SaveFileEntry> saves)
    {
        if (saves.Count == 0)
        {
            return new TransferResult { Success = false, Message = "没有可备份的存档。" };
        }

        var ok = 0;
        var fail = 0;
        foreach (var save in saves.Where(x => !x.IsMeta))
        {
            try
            {
                BackupIfExists(save.FullPath, "manual-backup");
                ok++;
            }
            catch
            {
                fail++;
            }
        }

        return new TransferResult
        {
            Success = fail == 0,
            Message = fail == 0 ? $"已创建 {ok} 个备份。" : $"已创建 {ok} 个备份，{fail} 个失败。",
        };
    }

    public TransferResult ResolveConflicts(IReadOnlyList<SaveSyncIssue> issues, bool keepSource)
    {
        if (issues.Count == 0)
        {
            return new TransferResult { Success = false, Message = "没有冲突可处理。" };
        }

        var ok = 0;
        var fail = 0;
        foreach (var issue in issues.Where(x => x.Type == SaveSyncIssueType.Conflict))
        {
            try
            {
                if (keepSource || string.IsNullOrWhiteSpace(issue.BackupPath) || !File.Exists(issue.BackupPath))
                {
                    BackupIfExists(issue.Save.FullPath, "resolve-conflict-keep-source");
                }
                else
                {
                    BackupIfExists(issue.Save.FullPath, "resolve-conflict-restore-backup");
                    File.Copy(issue.BackupPath, issue.Save.FullPath, overwrite: true);
                }

                ok++;
            }
            catch
            {
                fail++;
            }
        }

        return new TransferResult
        {
            Success = fail == 0,
            Message = fail == 0 ? $"冲突处理完成：{ok} 项。" : $"冲突处理完成：成功 {ok}，失败 {fail}。",
        };
    }

    public TransferResult MoveSavePosition(SavePackageContext package, SaveFileEntry save, string direction)
    {
        try
        {
            if (save.IsMeta)
            {
                return new TransferResult { Success = false, Message = "Meta 文件不支持移动档位。" };
            }

            var siblings = package.Saves
                .Where(x => !x.IsMeta && string.Equals(GetGroupToken(x), GetGroupToken(save), StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Slot)
                .ToList();

            if (siblings.Count == 0)
            {
                return new TransferResult { Success = false, Message = "未找到同组存档。" };
            }

            var minSlot = siblings.Min(x => x.Slot);
            var maxSlot = siblings.Max(x => x.Slot);
            var newSlot = direction.Equals("left", StringComparison.OrdinalIgnoreCase) ? save.Slot - 1 : save.Slot + 1;
            if (newSlot < minSlot)
            {
                newSlot = maxSlot;
            }
            else if (newSlot > maxSlot)
            {
                newSlot = minSlot;
            }

            var targetPlatform = package.Platform == SavePlatform.Unknown ? SavePlatform.Oc2Binary : package.Platform;
            var moved = SaveFileNameHelper.WithSlot(save, newSlot);
            var newName = SaveFileNameHelper.BuildFileName(targetPlatform, moved);
            var newPath = Path.Combine(package.PackagePath, newName);
            if (PathsEqual(save.FullPath, newPath))
            {
                return new TransferResult { Success = true, Message = "档位未发生变化。", TargetPath = newPath };
            }

            var occupied = siblings.FirstOrDefault(x => x.Slot == newSlot);
            if (occupied is not null && !PathsEqual(occupied.FullPath, save.FullPath))
            {
                BackupIfExists(save.FullPath, "move-slot");
                BackupIfExists(occupied.FullPath, "move-slot");
                var occupiedMoved = SaveFileNameHelper.WithSlot(occupied, save.Slot);
                var occupiedName = SaveFileNameHelper.BuildFileName(targetPlatform, occupiedMoved);
                var occupiedNewPath = Path.Combine(package.PackagePath, occupiedName);

                var tmp = occupied.FullPath + ".swap_tmp";
                File.Move(occupied.FullPath, tmp, overwrite: true);
                File.Move(save.FullPath, newPath, overwrite: true);
                File.Move(tmp, occupiedNewPath, overwrite: true);
            }
            else
            {
                BackupIfExists(save.FullPath, "move-slot");
                File.Move(save.FullPath, newPath, overwrite: true);
            }

            AppLogger.Info($"Move position: {save.FullPath} => {newPath}");
            return new TransferResult
            {
                Success = true,
                Message = $"档位已调整为 {newSlot}",
                TargetPath = newPath,
            };
        }
        catch (Exception ex)
        {
            AppLogger.Error("Move save position failed.", ex);
            return new TransferResult { Success = false, Message = ex.Message };
        }
    }

    public TransferResult CreateSaveWithPreset(
        SavePackageContext package,
        int slot,
        int? dlcId,
        string preset,
        SaveFileEntry? template = null)
    {
        try
        {
            if (slot < 0)
            {
                return new TransferResult { Success = false, Message = "档位不能为负数。" };
            }

            var probe = template ?? package.Saves.FirstOrDefault(x => !x.IsMeta && (dlcId == null || x.DlcId == dlcId));
            if (probe is null)
            {
                return new TransferResult
                {
                    Success = false,
                    Message = "当前分组没有可用模板存档，无法新建。请先保证该分组至少有一个可读存档。",
                };
            }

            var probeGroup = GetGroupToken(probe);
            var sameGroupTemplate = package.Saves
                .Where(x => !x.IsMeta && string.Equals(GetGroupToken(x), probeGroup, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Slot)
                .FirstOrDefault();
            if (sameGroupTemplate is null)
            {
                return new TransferResult
                {
                    Success = false,
                    Message = "未找到同分组模板存档，已取消新建以避免关卡结构损坏。",
                };
            }

            string json;
            try
            {
                json = ReadSaveAsJson(package, sameGroupTemplate);
            }
            catch (Exception ex)
            {
                return new TransferResult
                {
                    Success = false,
                    Message = $"模板存档读取失败: {ex.Message}",
                };
            }

            var presetJson = ApplyPreset(json, preset);
            var effectiveDlc = dlcId ?? sameGroupTemplate.DlcId;
            var effectivePrefix = effectiveDlc.HasValue ? string.Empty : sameGroupTemplate.Prefix;
            var baseEntry = new SaveFileEntry
            {
                FileName = "CoopSlot_SaveFile_0.save",
                FullPath = string.Empty,
                Size = 0,
                LastWriteTime = DateTime.Now,
                Slot = slot,
                DlcId = effectiveDlc,
                IsMeta = false,
                StarCount = null,
                Prefix = effectivePrefix,
            };
            var fileName = SaveFileNameHelper.BuildFileName(package.Platform, baseEntry);
            var fullPath = Path.Combine(package.PackagePath, fileName);
            if (File.Exists(fullPath))
            {
                return new TransferResult { Success = false, Message = $"目标档位已存在: {fileName}" };
            }

            WriteConvertedPayload(package, package.Platform, fullPath, presetJson);
            AppLogger.Info($"Created save preset={preset}, path={fullPath}");
            return new TransferResult { Success = true, Message = "新建存档成功", TargetPath = fullPath };
        }
        catch (Exception ex)
        {
            AppLogger.Error("Create save with preset failed.", ex);
            return new TransferResult { Success = false, Message = ex.Message };
        }
    }

    public IReadOnlyList<SaveBackupEntry> GetBackupHistory(SaveFileEntry save, int? maxCount = null)
    {
        if (string.IsNullOrWhiteSpace(save.FullPath))
        {
            return Array.Empty<SaveBackupEntry>();
        }

        var dir = Path.GetDirectoryName(save.FullPath);
        if (string.IsNullOrWhiteSpace(dir))
        {
            return Array.Empty<SaveBackupEntry>();
        }

        var backupRoot = Path.Combine(dir, ".overcookedtool-backup");
        if (!Directory.Exists(backupRoot))
        {
            return Array.Empty<SaveBackupEntry>();
        }

        var pattern = $"{save.FileName}.*.bak";
        var items = new List<SaveBackupEntry>();
        foreach (var path in Directory.GetFiles(backupRoot, pattern, SearchOption.TopDirectoryOnly))
        {
            var info = new FileInfo(path);
            ParseBackupMetadata(save.FileName, info.Name, out var parsedTime, out var reason);
            items.Add(new SaveBackupEntry
            {
                BackupPath = path,
                CreatedAt = parsedTime ?? info.LastWriteTime,
                Size = info.Exists ? info.Length : 0,
                Reason = DescribeBackupReason(reason),
            });
        }

        var keep = Math.Max(1, maxCount ?? BackupHistoryPerSave);
        return items
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.BackupPath, StringComparer.OrdinalIgnoreCase)
            .Take(keep)
            .ToList();
    }

    public TransferResult RestoreBackup(SaveFileEntry targetSave, string backupPath)
    {
        try
        {
            if (!File.Exists(backupPath))
            {
                return new TransferResult { Success = false, Message = "备份文件不存在。" };
            }

            var targetDir = Path.GetDirectoryName(targetSave.FullPath);
            if (string.IsNullOrWhiteSpace(targetDir))
            {
                return new TransferResult { Success = false, Message = "目标存档路径无效。" };
            }

            Directory.CreateDirectory(targetDir);
            BackupIfExists(targetSave.FullPath, "restore-history");
            File.Copy(backupPath, targetSave.FullPath, overwrite: true);
            AppLogger.Info($"Restore backup: {backupPath} => {targetSave.FullPath}");

            return new TransferResult
            {
                Success = true,
                Message = "已恢复到选中的历史版本。",
                TargetPath = targetSave.FullPath,
            };
        }
        catch (Exception ex)
        {
            AppLogger.Error("Restore backup failed.", ex);
            return new TransferResult { Success = false, Message = ex.Message };
        }
    }

    private static void WriteConvertedPayload(
        SavePackageContext targetPackage,
        SavePlatform targetPlatform,
        string targetPath,
        string convertedJson)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        if (targetPlatform is SavePlatform.AyceJson or SavePlatform.SwitchJson)
        {
            File.WriteAllText(targetPath, convertedJson, new UTF8Encoding(false));
            return;
        }

        if (string.IsNullOrWhiteSpace(targetPackage.DetectedKey))
        {
            throw new InvalidOperationException("目标存档包密钥未知，无法写入二进制存档。");
        }

        var encrypted = OvercookedCrypto.EncryptData(Encoding.UTF8.GetBytes(convertedJson), targetPackage.DetectedKey)
                        ?? throw new InvalidOperationException("加密到目标存档失败。");
        File.WriteAllBytes(targetPath, encrypted);
    }

    private void BackupIfExists(string filePath, string reason)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        var backupRoot = Path.Combine(Path.GetDirectoryName(filePath)!, ".overcookedtool-backup");
        Directory.CreateDirectory(backupRoot);
        var reasonTag = NormalizeBackupReason(reason);
        var backupName = $"{Path.GetFileName(filePath)}.{DateTime.Now:yyyyMMddHHmmssfff}.{reasonTag}.bak";
        var backupPath = Path.Combine(backupRoot, backupName);
        File.Copy(filePath, backupPath, overwrite: true);
        CleanupBackupHistory(backupRoot, Path.GetFileName(filePath), Math.Max(1, BackupHistoryPerSave));
        AppLogger.Info($"Backup[{reasonTag}] {filePath} => {backupPath}");
    }

    private static string NormalizeBackupReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "manual";
        }

        var sb = new StringBuilder(reason.Length);
        foreach (var c in reason.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_')
            {
                sb.Append(c);
            }
        }

        return sb.Length == 0 ? "manual" : sb.ToString();
    }

    private static bool ParseBackupMetadata(string sourceFileName, string backupFileName, out DateTime? timestamp, out string reason)
    {
        timestamp = null;
        reason = "unknown";
        if (!backupFileName.StartsWith(sourceFileName + ".", StringComparison.OrdinalIgnoreCase)
            || !backupFileName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var start = sourceFileName.Length + 1;
        var length = backupFileName.Length - start - 4;
        if (length <= 0)
        {
            return false;
        }

        var token = backupFileName.Substring(start, length);
        var split = token.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (split.Length == 0)
        {
            return false;
        }

        if (DateTime.TryParseExact(
                split[0],
                ["yyyyMMddHHmmssfff", "yyyyMMddHHmmss"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var ts))
        {
            timestamp = ts;
        }

        if (split.Length > 1 && !string.IsNullOrWhiteSpace(split[1]))
        {
            reason = split[1];
        }

        return true;
    }

    private static string DescribeBackupReason(string reason)
    {
        return reason switch
        {
            "edit" => "编辑前备份",
            "edit-sync" => "同步编辑到源文件前备份",
            "delete" => "删除前备份",
            "move-slot" => "调整档位前备份",
            "move-overwrite-target" => "移动覆盖目标前备份",
            "copy-overwrite-target" => "复制覆盖目标前备份",
            "move-delete-source" => "移动删除源文件前备份",
            "manual-backup" => "手动备份",
            "sync-create" => "同步时创建基线备份",
            "sync-restore" => "同步还原前备份",
            "sync-refresh" => "同步刷新备份",
            "restore-history" => "恢复历史前备份",
            "resolve-conflict-keep-source" => "冲突处理（保留源文件）",
            "resolve-conflict-restore-backup" => "冲突处理（保留备份文件）",
            _ => reason,
        };
    }

    private static void CleanupBackupHistory(string backupRoot, string sourceFileName, int maxKeep)
    {
        if (maxKeep <= 0 || !Directory.Exists(backupRoot))
        {
            return;
        }

        var pattern = $"{sourceFileName}.*.bak";
        var files = Directory.GetFiles(backupRoot, pattern, SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(x =>
            {
                ParseBackupMetadata(sourceFileName, x.Name, out var timestamp, out _);
                return timestamp ?? x.LastWriteTime;
            })
            .ThenByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count <= maxKeep)
        {
            return;
        }

        foreach (var extra in files.Skip(maxKeep))
        {
            try
            {
                extra.Delete();
            }
            catch
            {
                // ignore cleanup failures
            }
        }
    }

    private void SyncBackupHistoryForTransfer(SaveFileEntry sourceSave, string targetPath, bool move)
    {
        var sourceDir = Path.GetDirectoryName(sourceSave.FullPath);
        var targetDir = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrWhiteSpace(sourceDir) || string.IsNullOrWhiteSpace(targetDir))
        {
            return;
        }

        var sourceBackupRoot = Path.Combine(sourceDir, ".overcookedtool-backup");
        if (!Directory.Exists(sourceBackupRoot))
        {
            return;
        }

        var targetBackupRoot = Path.Combine(targetDir, ".overcookedtool-backup");
        Directory.CreateDirectory(targetBackupRoot);

        var sourceFileName = sourceSave.FileName;
        var targetFileName = Path.GetFileName(targetPath);
        var pattern = $"{sourceFileName}.*.bak";
        foreach (var srcPath in Directory.GetFiles(sourceBackupRoot, pattern, SearchOption.TopDirectoryOnly))
        {
            var sourceBackupName = Path.GetFileName(srcPath);
            if (!ParseBackupMetadata(sourceFileName, sourceBackupName, out _, out _))
            {
                continue;
            }

            var suffix = sourceBackupName[(sourceFileName.Length + 1)..^4];
            var targetBackupName = $"{targetFileName}.{suffix}.bak";
            var targetBackupPath = Path.Combine(targetBackupRoot, targetBackupName);
            var dedup = 1;
            while (File.Exists(targetBackupPath))
            {
                targetBackupPath = Path.Combine(targetBackupRoot, $"{targetFileName}.{suffix}.{dedup}.bak");
                dedup++;
            }

            if (move)
            {
                File.Move(srcPath, targetBackupPath, overwrite: false);
            }
            else
            {
                File.Copy(srcPath, targetBackupPath, overwrite: false);
            }
        }

        CleanupBackupHistory(targetBackupRoot, targetFileName, Math.Max(1, BackupHistoryPerSave));
    }

    private static bool TryGetLatestBackup(SaveFileEntry save, out string backupPath, out DateTime backupTime)
    {
        backupPath = string.Empty;
        backupTime = DateTime.MinValue;

        var dir = Path.GetDirectoryName(save.FullPath);
        if (string.IsNullOrWhiteSpace(dir))
        {
            return false;
        }

        var backupRoot = Path.Combine(dir, ".overcookedtool-backup");
        if (!Directory.Exists(backupRoot))
        {
            return false;
        }

        var pattern = $"{save.FileName}.*.bak";
        var latest = Directory.GetFiles(backupRoot, pattern, SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(x =>
            {
                ParseBackupMetadata(save.FileName, x.Name, out var ts, out _);
                return ts ?? x.LastWriteTime;
            })
            .ThenByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (latest is null)
        {
            return false;
        }

        backupPath = latest.FullName;
        ParseBackupMetadata(save.FileName, latest.Name, out var parsedTs, out _);
        backupTime = parsedTs ?? latest.LastWriteTime;
        return true;
    }

    private static bool PathsEqual(string a, string b)
    {
        var x = Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var y = Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
    }

    private static int ComputeNextSlot(SaveFileEntry sourceSave, IReadOnlyList<SaveFileEntry> targetSaves)
    {
        var group = GetGroupToken(sourceSave);
        var maxSlot = targetSaves
            .Where(x => x.IsMeta == sourceSave.IsMeta && string.Equals(GetGroupToken(x), group, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Slot)
            .DefaultIfEmpty(sourceSave.Slot)
            .Max();
        return maxSlot + 1;
    }

    private static string GetGroupToken(SaveFileEntry save)
    {
        if (save.DlcId.HasValue)
        {
            return $"DLC{save.DlcId.Value}";
        }

        if (!string.IsNullOrWhiteSpace(save.Prefix))
        {
            return save.Prefix;
        }

        return "CoopSlot";
    }

    private SaveVersion DetectPackageVersion(SavePlatform platform, IReadOnlyList<SaveFileEntry> entries, string? key)
    {
        var probe = entries.FirstOrDefault(x => !x.IsMeta) ?? entries.FirstOrDefault();
        if (probe is null)
        {
            return SaveVersion.Unknown;
        }

        try
        {
            string jsonText;
            if (platform is SavePlatform.AyceJson or SavePlatform.SwitchJson)
            {
                jsonText = File.ReadAllText(probe.FullPath, Encoding.UTF8).TrimEnd('\0');
            }
            else
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    return SaveVersion.Unknown;
                }

                var bytes = File.ReadAllBytes(probe.FullPath);
                if (!OvercookedCrypto.TryDecryptToJsonText(bytes, key, out jsonText))
                {
                    return SaveVersion.Unknown;
                }
            }

            return SaveJsonConverter.DetectVersion(jsonText);
        }
        catch
        {
            return SaveVersion.Unknown;
        }
    }

    private IReadOnlyList<SaveFileEntry> PopulateStarCounts(
        IReadOnlyList<SaveFileEntry> entries,
        SavePlatform platform,
        string? key)
    {
        var output = new List<SaveFileEntry>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.IsMeta)
            {
                output.Add(entry);
                continue;
            }

            int? stars = null;
            try
            {
                string json;
                if (platform is SavePlatform.AyceJson or SavePlatform.SwitchJson)
                {
                    json = File.ReadAllText(entry.FullPath, Encoding.UTF8).TrimEnd('\0');
                }
                else if (!string.IsNullOrWhiteSpace(key))
                {
                    var bytes = File.ReadAllBytes(entry.FullPath);
                    if (!OvercookedCrypto.TryDecryptToJsonText(bytes, key, out json))
                    {
                        json = string.Empty;
                    }
                }
                else
                {
                    json = string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(json))
                {
                    stars = TryExtractStarCount(json);
                }
            }
            catch
            {
                // ignore star parse error
            }

            output.Add(new SaveFileEntry
            {
                FileName = entry.FileName,
                FullPath = entry.FullPath,
                Size = entry.Size,
                LastWriteTime = entry.LastWriteTime,
                Slot = entry.Slot,
                DlcId = entry.DlcId,
                IsMeta = entry.IsMeta,
                StarCount = stars,
                Prefix = entry.Prefix,
            });
        }

        return output;
    }

    private static int? TryExtractStarCount(string json)
    {
        var root = JsonNode.Parse(json)?.AsObject();
        var keys = root?["m_Keys"]?.AsArray();
        var entries = root?["m_Entries"]?.AsArray();
        if (keys is null || entries is null)
        {
            return null;
        }

        var count = Math.Min(keys.Count, entries.Count);
        var foundAny = false;
        var total = 0;
        for (var i = 0; i < count; i++)
        {
            var key = keys[i]?.GetValue<string>();
            if (!IsLevelDataKey(key))
            {
                continue;
            }

            var entryObj = entries[i] as JsonObject;
            var innerText = entryObj?["m_JSON"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(innerText))
            {
                continue;
            }

            var inner = JsonNode.Parse(innerText)?.AsObject();
            var map = ToInnerMap(inner);
            if (map.TryGetValue("ScoreStars", out var starsNode))
            {
                if (int.TryParse(starsNode?.ToString(), out var stars))
                {
                    total += stars;
                    foundAny = true;
                }
            }
        }

        return foundAny ? total : null;
    }

    private static bool IsLevelDataKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("Level_", StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(key.AsSpan("Level_".Length), out _);
    }

    private static bool IsSavePackageDirectory(string path)
    {
        try
        {
            return Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(x => x is not null)
                .Cast<string>()
                .Any(IsRecognizedSaveFileName);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsRecognizedSaveFileName(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        if (lower.EndsWith(".save", StringComparison.Ordinal) || lower.EndsWith(".json", StringComparison.Ordinal) || lower.EndsWith(".sjson", StringComparison.Ordinal))
        {
            return lower.Contains("savefile", StringComparison.Ordinal) || lower.StartsWith("meta", StringComparison.Ordinal);
        }

        var upper = fileName.ToUpperInvariant();
        return upper.Contains("CAMPAIGNSAVE", StringComparison.Ordinal)
               || upper.Equals("META", StringComparison.Ordinal)
               || (upper.StartsWith("DLC", StringComparison.Ordinal) && upper.Contains("CAMPAIGNSAVE", StringComparison.Ordinal));
    }

    private static SavePlatform DetectPlatform(IReadOnlyList<SaveFileEntry> entries)
    {
        var names = entries.Select(x => x.FileName).ToList();
        if (names.Any(x => x.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            return SavePlatform.AyceJson;
        }

        if (names.Any(x => x.EndsWith(".save", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".sjson", StringComparison.OrdinalIgnoreCase)))
        {
            return SavePlatform.Oc2Binary;
        }

        if (names.Any(x => x.Contains("CAMPAIGNSAVE", StringComparison.OrdinalIgnoreCase) || x.Equals("meta", StringComparison.OrdinalIgnoreCase)))
        {
            var probe = entries.FirstOrDefault(x => !x.IsMeta) ?? entries.FirstOrDefault();
            if (probe is not null && IsPlainJsonFile(probe.FullPath))
            {
                return SavePlatform.SwitchJson;
            }

            return SavePlatform.XboxBinary;
        }

        return SavePlatform.Unknown;
    }

    private static bool IsPlainJsonFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true);
            var content = reader.ReadToEnd().TrimStart('\0', ' ', '\r', '\n', '\t');
            if (!content.StartsWith("{", StringComparison.Ordinal))
            {
                return false;
            }

            using var _ = JsonDocument.Parse(content);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ApplyPreset(string json, string preset)
    {
        var root = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("模板 JSON 无效。");
        var keys = root["m_Keys"]?.AsArray() ?? throw new InvalidOperationException("模板 JSON 缺少 m_Keys。");
        var entries = root["m_Entries"]?.AsArray() ?? throw new InvalidOperationException("模板 JSON 缺少 m_Entries。");

        var count = Math.Min(keys.Count, entries.Count);
        for (var i = 0; i < count; i++)
        {
            var key = keys[i]?.GetValue<string>();
            if (!IsLevelDataKey(key))
            {
                continue;
            }

            var outer = entries[i] as JsonObject;
            var innerText = outer?["m_JSON"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(innerText))
            {
                continue;
            }

            var inner = JsonNode.Parse(innerText)?.AsObject();
            var map = ToInnerMap(inner);
            if (map.Count == 0)
            {
                continue;
            }

            switch (preset)
            {
                case "通关存档":
                    map["ScoreStars"] = "4";
                    map["Completed"] = "True";
                    map["Purchased"] = "True";
                    map["Revealed"] = "True";
                    map["HighScore"] = map.TryGetValue("HighScore", out var _) ? map["HighScore"] : "3000";
                    break;
                default:
                    map["ScoreStars"] = "0";
                    map["Completed"] = "False";
                    map["Purchased"] = "False";
                    map["Revealed"] = "True";
                    map["HighScore"] = "0";
                    break;
            }

            SetInnerMap(inner!, map);
            outer!["m_JSON"] = inner!.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        }

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static Dictionary<string, JsonNode?> ToInnerMap(JsonObject? inner)
    {
        var result = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        var keyArray = inner?["m_Key"] as JsonArray;
        var valueArray = inner?["m_Value"] as JsonArray;
        if (keyArray is null || valueArray is null)
        {
            return result;
        }

        var count = Math.Min(keyArray.Count, valueArray.Count);
        for (var i = 0; i < count; i++)
        {
            var key = keyArray[i]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = valueArray[i]?.DeepClone();
            }
        }

        return result;
    }

    private static void SetInnerMap(JsonObject inner, Dictionary<string, JsonNode?> map)
    {
        var keys = new JsonArray();
        var values = new JsonArray();
        foreach (var pair in map)
        {
            keys.Add(pair.Key);
            values.Add(pair.Value?.DeepClone());
        }

        inner["m_Key"] = keys;
        inner["m_Value"] = values;
    }
}
