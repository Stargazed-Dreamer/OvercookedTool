# 架构设计（Architecture）

本文描述「胡闹厨房存档管理器（OvercookedTool）」的整体架构、模块职责、关键流程、数据结构与加密细节，供维护者快速建立全局认知。代码引用均使用相对仓库根目录的路径。

## 1. 整体架构

项目分为三层：上层 WinForms 应用负责交互，中层核心服务编排存档业务，底层加密与模型提供基础能力。

```
┌─────────────────────────────────────────────────────────────┐
│  OvercookedTool.App (net9.0-windows, WinForms)              │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ MainForm / PackageTabView       多标签页主界面        │  │
│  │ JsonEditorForm / SaveTableEditorForm / MetaTableEditor│  │
│  │ SaveTimelineForm                历史时间线             │  │
│  │ ImportPackageDialog / SelectPackageDialog / AddSave…  │  │
│  │ SettingsForm / AboutForm / DonateForm                 │  │
│  │ Program.cs                     入口+全局异常          │  │
│  └───────────────────────────────────────────────────────┘  │
│                          │ 调用                              │
├──────────────────────────▼──────────────────────────────────┤
│  OvercookedTool.Core (net9.0, 类库)                         │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ Services/                                            │  │
│  │   SavePackageService   包加载/读写/复制移动/删除/    │  │
│  │                        同步/备份/历史/冲突           │  │
│  │   KeyDetector          密钥自动探测 + 好友号提取     │  │
│  │   SaveJsonConverter    OC2↔AYCE 版本检测与转换       │  │
│  │   SaveFileNameHelper   文件名解析与构建              │  │
│  │ Logging/AppLogger      按天日志 + 过期清理           │  │
│  └───────────────────────────────────────────────────────┘  │
│                          │ 依赖                              │
├──────────────────────────▼──────────────────────────────────┤
│  Crypto/OvercookedCrypto.cs   AES-CBC + CRC32 加解密        │
│  Models/                       SavePlatform/SaveVersion/    │
│                                SaveFileEntry/SavePackageCtx │
│                                TransferResult/SaveBackup…   │  │
└─────────────────────────────────────────────────────────────┘
```

调用方向：`App` → `Services` → `Crypto` / `Models`。`App` 不直接调用 `Crypto`，所有加解密都经由 `SavePackageService` 完成，便于统一备份与异常处理。

## 2. 模块职责

| 模块 | 文件 | 职责 |
|---|---|---|
| 加解密 | `OvercookedTool.Core/Crypto/OvercookedCrypto.cs` | AES-256-CBC 加解密、CRC32 校验、`PasswordDeriveBytes` 派生密钥、`TryDecryptToJsonText` 验证有效 JSON |
| 密钥探测 | `OvercookedTool.Core/Services/KeyDetector.cs` | 构建候选密钥列表并依次尝试真实解密；从 `steam_autocloud.vdf` 提取好友号 |
| 存档包服务 | `OvercookedTool.Core/Services/SavePackageService.cs` | 包加载、平台/版本检测、星级填充、读写存档、复制/移动自动转换、删除、调整档位、新建存档预设、备份历史、同步诊断、冲突检测、批量备份、恢复历史 |
| 版本转换 | `OvercookedTool.Core/Services/SaveJsonConverter.cs` | 检测存档版本（OC2/AYCE），按方向转换 `FailedAttempts` 与 `AssistModeEnabled` |
| 文件名解析 | `OvercookedTool.Core/Services/SaveFileNameHelper.cs` | 现代/旧版/meta 三正则解析；按平台与模板构建文件名；`WithSlot` 复制并改档位 |
| 日志 | `OvercookedTool.Core/Logging/AppLogger.cs` | 线程安全按天文件日志、按天节流清理过期日志、`LogEmitted` 事件 |
| 数据模型 | `OvercookedTool.Core/Models/` | `SavePlatform`、`SaveVersion`、`SaveFileEntry`、`SavePackageContext`、`TransferResult`、`SaveBackupEntry`、`SaveSyncIssue` |
| 应用入口 | `OvercookedTool.App/Program.cs` | 加载设置、初始化日志、注册全局异常处理、启动主窗体 |
| 设置 | `OvercookedTool.App/AppSettings.cs` | 最近路径、自动检测、日志开关、最近历史条数、备份数、日志保留天数、Unity 设备标识 |

## 3. 关键流程

### 3.1 加载存档包

入口：`SavePackageService.LoadPackage(packagePath, preferredKey, allowEmpty, unityDeviceId)`。

```
1. 校验目录存在；扫描顶层文件，过滤"可识别存档文件名"
   （IsRecognizedSaveFileName：.save/.json/.sjson 含 savefile 或 meta，或含 CAMPAIGNSAVE）
2. 对每个文件调用 SaveFileNameHelper.TryParse 解析为 SaveFileEntry
3. DetectPlatform(entries)：按扩展名与内容判定 Oc2Binary / AyceJson / XboxBinary / SwitchJson
   - .json → AyceJson
   - .save/.sjson → Oc2Binary
   - 含 CAMPAIGNSAVE/meta 且为明文 JSON → SwitchJson，否则 XboxBinary
4. KeyDetector.DetectKey：构建候选密钥并取首个非 Meta 存档逐个尝试解密
5. DetectPackageVersion：读取首个非 Meta 存档，调用 SaveJsonConverter.DetectVersion
6. KeyDetector.TryExtractFriendCode：从 steam_autocloud.vdf 提取 accountid 作为好友号
7. PopulateStarCounts：解密每个存档，累加 Level_* 的 ScoreStars 得到总星数
8. 返回 SavePackageContext（Saves 按 IsMeta→DlcId→Slot→FileName 排序）
```

### 3.2 读写存档

读：`ReadSaveAsJson(package, save)`

- JSON 平台（AyceJson / SwitchJson）：直接 `File.ReadAllText` 并 `TrimEnd('\0')`。
- 二进制平台：用 `package.DetectedKey` 调 `OvercookedCrypto.TryDecryptToJsonText`，失败抛异常。

写：`WriteJsonToSave(package, save, jsonText, backupReason="edit")`

```
1. 确保目标目录存在
2. BackupIfExists(save.FullPath, backupReason)  // 写入前自动备份
3. JSON 平台：以无 BOM UTF-8 写入文本
4. 二进制平台：Encoding.UTF8.GetBytes → OvercookedCrypto.EncryptData → File.WriteAllBytes
```

### 3.3 复制/移动自动转换

入口：`TransferSave(sourcePackage, sourceSave, targetDirectory, move)`。

```
1. 加载目标包（allowEmpty=true）；目标平台未知则默认 Oc2Binary
2. 确定目标版本：AyceJson 平台→Ayce，否则取目标包版本，未知→Oc2
3. 读取源存档 JSON，DetectVersion；未知则回退到源包版本
4. SaveJsonConverter.Convert(sourceJson, sourceVersion, targetVersion)
5. SaveFileNameHelper.BuildFileName(targetPlatform, sourceSave) 生成目标文件名
6. 若源==目标同路径，则用 ComputeNextSlot 改档位避免覆盖自身
7. BackupIfExists(targetPath, move ? "move-overwrite-target" : "copy-overwrite-target")
8. WriteConvertedPayload：按目标平台加密落盘或直接写 JSON
9. SyncBackupHistoryForTransfer：把源存档的历史备份迁移/复制到目标目录
10. 若 move：BackupIfExists(source, "move-delete-source") 后 File.Delete
11. 返回 TransferResult { Success, Message, TargetPath }
```

### 3.4 密钥探测候选顺序

`KeyDetector.BuildCandidates` 按以下顺序构建候选列表（去重、去空白）：

1. **手动输入密钥**（`preferredKey`，来源标记“手动输入密钥”）
2. **Unity 设备标识**（`unityDeviceId`，来源“Unity设备标识”；由 `UnityHarness` 采集 `SystemInfo.deviceUniqueIdentifier`）
3. **目录名**（存档包所在目录名，来源“目录名”）
4. **`steam_autocloud.vdf` 的 `accountid`**（正则 `"accountid"\s*"(\d+)"`，来源“steam_autocloud.vdf/accountid”）
5. **SteamID64**（`76561197960265728 + accountid`，来源“steam_autocloud.vdf/steamid64”）
6. **父目录数字 ID**（向上最多 4 层，匹配 `^\d{6,}$`，来源“父目录数字ID”）
7. **Epic 回退密钥**（常量 `Epic.OnlineServices.EpicAccountId`，来源“Epic常见密钥”）

> JSON 平台（AyceJson / SwitchJson）无需密钥，直接返回成功。若无可探测存档，则取首个候选作为“未验证”回退。

### 3.5 版本转换差异

`SaveJsonConverter.DetectVersion` 以存档 `m_Keys` 中是否存在 `AssistModeEnabled` 键判版本：存在→`Ayce`，否则→`Oc2`。

`SaveJsonConverter.Convert` 仅处理 `Level_*` 关卡条目，差异字段：

| 字段 | OC2 → AYCE | AYCE → OC2 |
|---|---|---|
| `FailedAttempts` | 在关卡内层 map 补 `"0"` | 从内层 map 移除 |
| `AssistModeEnabled` | 在 `m_Keys`/`m_Entries` 末尾新增条目，值为 `false` | 移除对应键与条目 |

### 3.6 备份系统

- **位置**：存档所在目录下的 `.overcookedtool-backup/`。
- **命名**：`{文件名}.{yyyyMMddHHmmssfff}.{reason}.bak`，`reason` 经 `NormalizeBackupReason` 规范化（小写、仅字母数字与 `-`/`_`）。
- **触发点**：编辑、复制/移动覆盖目标、移动删除源、删除、调整档位、新建存档前对模板的备份、同步、冲突处理、恢复历史。
- **保留数清理**：`CleanupBackupHistory` 按文件名解析时间戳，按时间倒序保留最近 N 份（`BackupHistoryPerSave`，默认 10，由 `AppSettings.MaxBackupPerSave` 配置），多余删除。
- **迁移**：复制/移动时 `SyncBackupHistoryForTransfer` 把源存档的历史备份按目标文件名重命名后迁移（move）或复制（copy）到目标目录的 `.overcookedtool-backup/`，重名时追加 `.{n}.bak` 去重。
- **历史读取**：`GetBackupHistory(save, maxCount)` 列出备份并解析时间戳与原因；`RestoreBackup` 在恢复前对当前文件再做一次“restore-history”备份。

## 4. 数据结构

### 4.1 SavePackageContext（`Models/SavePackageContext.cs`）

| 属性 | 类型 | 说明 |
|---|---|---|
| `PackagePath` | `string` | 存档包目录绝对路径 |
| `DisplayName` | `string` | 标签页显示名（目录名） |
| `Platform` | `SavePlatform` | 检测到的平台 |
| `Version` | `SaveVersion` | 检测到的存档版本 |
| `DetectedKey` | `string?` | 探测到的密钥（JSON 平台为 null） |
| `KeySource` | `string` | 密钥来源描述 |
| `KeyValidated` | `bool` | 密钥是否已通过真实解密验证 |
| `FriendCode` | `string?` | 从 `steam_autocloud.vdf` 提取的好友号 |
| `Saves` | `IReadOnlyList<SaveFileEntry>` | 排序后的存档条目列表 |

### 4.2 SaveFileEntry（`Models/SaveFileEntry.cs`）

| 属性 | 说明 |
|---|---|
| `FileName` / `FullPath` | 文件名与绝对路径 |
| `Size` / `LastWriteTime` | 文件大小与最后修改时间 |
| `Slot` | 档位编号 |
| `DlcId` | DLC 编号（可空） |
| `IsMeta` | 是否为 Meta 存档 |
| `StarCount` | 关卡总星数（可空，解析失败为 null） |
| `Prefix` | 文件名前缀（如组名） |
| `Group` | 计算属性：`DlcId.HasValue ? "DLC{id}" : (Prefix 非空 ? Prefix : "CoopSlot")` |

### 4.3 枚举

`SavePlatform`（`Models/SavePlatform.cs`）：`Unknown=0`、`Oc2Binary=1`、`AyceJson=2`、`XboxBinary=3`、`SwitchJson=4`。

`SaveVersion`（`Models/SaveVersion.cs`）：`Unknown=0`、`Oc2=1`、`Ayce=2`。

### 4.4 TransferResult（`Models/TransferResult.cs`）

`{ Success: bool, Message: string, TargetPath: string? }`，作为所有写操作的统一返回结构。

## 5. 加密细节

实现见 `OvercookedTool.Core/Crypto/OvercookedCrypto.cs`。

- **算法**：AES-256-CBC（代码用 `RijndaelManaged`，`Mode = CipherMode.CBC`）。
- **密钥派生**：`PasswordDeriveBytes(password, SaltBytes, "SHA1", 2)`，取 32 字节（256 位）密钥。盐值为硬编码 ASCII 字符串。`PasswordDeriveBytes` 在 .NET 9 标记为过时（`SYSLIB0041`），代码以 `#pragma` 显式抑制以保持与游戏原始实现兼容。
- **IV**：16 字节，**前置**于密文头部；加密时 `Random.Shared.NextBytes` 生成，解密时从头部读取。
- **填充**：PKCS7（`RijndaelManaged` 默认）；解密用 `TransformFinalBlock` 自动去填充，得到真实明文长度。
- **CRC32**：**后置** 4 字节，多项式 `1491524015`、种子 `3605721660`，使用预计算查找表。加密时计算 `IV+密文` 的 CRC 追加到末尾；解密时先 `Crc32.Validate` 校验前 `Length-4` 字节（可由 `ignoreCrc` 跳过）。
- **文件布局**：`[IV(16)] [密文(变长, PKCS7)] [CRC32(4)]`。
- **JSON 校验**：`TryDecryptToJsonText` 解密后 `TrimEnd('\0')` 并尝试 `JsonDocument.Parse`，只有能解析为有效 JSON 才视为密钥正确。
- **数据长度门槛**：`DecryptData` 要求长度 > 20（16 IV + 4 CRC）。

## 6. 日志

实现见 `OvercookedTool.Core/Logging/AppLogger.cs`。

- **文件**：`{logDirectory}/overcookedtool-{yyyyMMdd}.log`，按天一个文件，UTF-8 追加写入。
- **行格式**：`[yyyy-MM-dd HH:mm:ss.fff] [LEVEL] message`，级别 `INFO` / `WARN` / `ERROR`。
- **线程安全**：所有写入在 `SyncRoot` 锁内。
- **过期清理**：`TryPurgeExpiredLogs` 按天节流（同一天只清理一次），按文件名日期解析，删除早于 `today - MaxRetentionDays` 的日志；`0` 表示永不清理。无法解析日期的文件保留不动，避免误删非本工具日志。
- **配置**：`AppLogger.Initialize(logDirectory, enabled, maxRetentionDays)`，由 `Program.cs` 在启动时根据 `AppSettings` 调用。
- **事件**：`LogEmitted` 在每行写入后触发，UI 可订阅用于实时显示。
