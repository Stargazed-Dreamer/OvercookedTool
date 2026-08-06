# 变更日志（Changelog）

本项目遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/) 格式，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)，与 `Directory.Build.props` 中的 `Version` 对齐。

## [1.0.0] - 2026-08-07

首个对齐到 `Directory.Build.props` 版本号 `1.0.0` 的可发布版本。本项目由原 Python + C# 混合实现（旧仓库 `OvercookSavesEditor`，UI 使用 PySide6，加解密依赖 `OvercookedLib.dll` + pythonnet）迁移为纯 C# .NET 9 解决方案，详见 `迁移说明.md`。

### Added

- 工程基础设施补全：新增 `.editorconfig` 统一缩进/换行/命名风格；新增 `Directory.Build.props` 集中产品信息、版本号（1.0.0）、MIT 协议、语言特性与 `zh-CN` 本地化；完善 `.gitignore` 规则。
- 取消跟踪 `obj/` 中间产物与真实存档样本，仓库不再包含任何含个人账户信息的真实存档，改用合成 fixtures 支撑测试。
- 加密与存档核心（`OvercookedTool.Core`）：
  - `Crypto/OvercookedCrypto.cs`：AES-256-CBC 解密/加密、IV 16 字节前置、CRC32 后置 4 字节校验、`PasswordDeriveBytes`(SHA1, 2 iter) 派生密钥、PKCS7 填充、`TryDecryptToJsonText` 校验有效 JSON。
  - `Services/KeyDetector.cs`：候选密钥自动探测，依次尝试手动密钥 → Unity 设备标识 → 目录名 → `steam_autocloud.vdf` 的 `accountid` → SteamID64（`76561197960265728 + accountid`）→ 父目录数字 ID → Epic 常见回退密钥；并支持从 `steam_autocloud.vdf` 提取好友号。
  - `Services/SavePackageService.cs`：包加载、平台检测、版本检测、星级填充、读写存档（含备份）、复制/移动自动版本转换、删除、调整档位（含同组交换）、新建存档预设、备份历史管理、同步诊断、冲突检测、批量备份、恢复历史版本。
  - `Services/SaveJsonConverter.cs`：OC2 ↔ AYCE 版本检测与转换，处理 `FailedAttempts`（OC2→AYCE 补 `"0"`，AYCE→OC2 移除）与 `AssistModeEnabled`（OC2→AYCE 新增条目默认 `false`，AYCE→OC2 移除条目）。
  - `Services/SaveFileNameHelper.cs`：现代/旧版/meta 三种文件名正则解析与按平台构建文件名。
  - `Logging/AppLogger.cs`：按天文件日志（`overcookedtool-yyyyMMdd.log`），按天节流清理过期日志，可配保留天数（0 表示永不清理）。
  - `Models/`：`SavePlatform`、`SaveVersion`、`SaveFileEntry`、`SavePackageContext`、`TransferResult`、`SaveBackupEntry`、`SaveSyncIssue` 等数据模型。
- WinForms 桌面应用（`OvercookedTool.App`）：
  - `Program.cs` 入口与全局异常处理（`AppDomain.UnhandledException`、`Application.ThreadException`、`TaskScheduler.UnobservedTaskException`）。
  - 多标签页主界面、导入存档包、拖拽打开、自动检测候选路径、手动密钥输入、JSON 编辑器、表格编辑器、Meta 编辑器、存档卡片矩阵、调整档位、新建存档、历史时间线、同步诊断、设置、关于、捐赠等窗体。
  - 首次启动 Unity 设备标识 Harness（`UnityHarness/_UnityDeviceUniqueIdentifierHarness.exe`）采集本机 `SystemInfo.deviceUniqueIdentifier`。
  - `AppSettings.cs` 设置项：最近路径、自动检测、日志开关、最近历史条数、备份数、日志保留天数、Unity 设备标识。
  - 资源文件：`about_content.json`（关于页内容）、`save_display_config.json`（关卡翻译/显示配置）、`tools.scan_translation_keys.ps1`（扫描未翻译键）、`libcoffee.dll`（收款码改后缀存储）。
- 测试套件（`OvercookedTool.Tests`，xUnit）：单元测试覆盖加密、密钥检测、文件名解析、版本转换；集成测试使用合成 fixtures，确保 CI 可运行；真实存档测试用 `SkippableFact` 在缺样本时自动跳过。
- 端到端测试指南 `docs/e2e-testing.md`（基于 LocalAgent Computer Use 的 WinForms 真实路径验证）。
- 文档套件：`README.md`、`CONTRIBUTING.md`、`docs/architecture.md`、`docs/development.md`、`docs/usage.md`。

### Changed

- 整体技术栈由 Python（PySide6 + pythonnet 调 `OvercookedLib.dll`）迁移为纯 C# .NET 9，解决 Python/C# 混用导致的维护复杂、文件散落问题（见 `迁移说明.md`）。
- 加解密实现由旧 `OvercookedLib` 迁移为 `OvercookedTool.Core.Crypto.OvercookedCrypto`，解密改用 `TransformFinalBlock` 直接获取真实明文长度，避免旧实现 `CryptoStream.Read` 写入 `new byte[cipher.Length]` 导致尾部多出 1–16 字节 `0x00`、需要 `TrimEnd('\0')` 的问题（保留 `TrimEnd('\0')` 以兼容历史数据）。
- OC2/AYCE 结构转换由旧 `transfer.py` 迁移为 `SaveJsonConverter.Convert`，以 `AssistModeEnabled` 键是否存在作为版本判据。
- 文件名解析由散落逻辑统一为 `SaveFileNameHelper` 三正则（现代 `CoopSlot_SaveFile_N` / 旧版 `CAMPAIGNSAVE` / 单独 `meta`）。
- 备份系统统一到 `.overcookedtool-backup/` 目录，命名 `{file}.{yyyyMMddHHmmssfff}.{reason}.bak`，按文件保留最近 N 份（默认 10），复制/移动时迁移历史备份。
- 仓库不再跟踪真实存档样本，避免泄露个人账户信息。

### Fixed

- 修复 `OvercookedTool.App.csproj` 文件头连续 4 个 UTF-8 BOM 导致构建失败的问题（2026-07-31 E2E 执行期间发现并修复，`dotnet test` 299 项：279 通过、20 跳过、0 失败；`dotnet build` 0 警告 0 错误）。
- 修复 Unity 机器码模块无法独立使用的问题，将其融合进项目作为 Harness（提交 `无法独立使用unity机器码模块，融合进项目`）。
- 修复群友报错场景（经核查为使用方问题，提交 `群友报错检查，是群友的问题`）。
- 恢复 App 构建并补全端到端测试文档（提交 `fix:restore-app-build-and-document-e2e-tests`）。

### Removed

- 移除原 Python 实现（PySide6 UI、`transfer.py`、`save_manager.py`、pythonnet 桥接）。
- 移除对独立 `OvercookedLib.dll` 的运行时依赖（加解密逻辑已内置到 `OvercookedTool.Core`）。
- 从仓库移除真实存档样本（含个人账户信息），改用合成 fixtures。

## 历史提交（迁移前与迁移期）

以下提交对应本项目从旧 Python 实现迁移到纯 C# 的过程，按时间倒序：

- `f1ed009` 创建测试
- `dd5af4e` fix:restore-app-build-and-document-e2e-tests
- `fda7987` 群友报错检查，是群友的问题
- `ccc25f9` AI加注释，可能有问题
- `996756e` 无法独立使用unity机器码模块，融合进项目
- `323a4c7` 可发布
- `c7d0141` 更新构建说明
- `304eeb3` init

[1.0.0]: https://github.com/OvercookedTool/OvercookedTool/releases/tag/v1.0.0
