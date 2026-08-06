# 胡闹厨房存档管理器（OvercookedTool）

胡闹厨房（Overcooked! 2 / Overcooked! All You Can Eat）存档管理器：多存档包标签页、自动识别平台与密钥、OC2 ↔ AYCE 版本转换、JSON 与表格编辑、备份与历史时间线、同步诊断与冲突检测。基于 .NET 9 WinForms 的纯 C# 桌面工具。

> 本项目由原 Python + C# 混合实现（PySide6 UI + `OvercookedLib.dll` + pythonnet）迁移为纯 C# .NET 9 解决方案，迁移历史见 [`迁移说明.md`](./迁移说明.md)。

## 功能特性

- **自动识别存档平台/格式**：OC2 二进制 `.save`、AYCE JSON `.json`、Xbox `CAMPAIGNSAVE`、Switch `.sjson`。
- **自动密钥探测**：依次尝试手动密钥 → Unity 设备标识 → 目录名 → `steam_autocloud.vdf` 的 `accountid` → SteamID64 → 父目录数字 ID → Epic 常见回退密钥，并通过真实解密校验。
- **版本转换**：复制/移动到目标包时自动检测 OC2 / AYCE 版本并转换（处理 `FailedAttempts` 与 `AssistModeEnabled` 差异）。
- **多标签页**：同时打开多个存档包，每个包一个标签页，互不干扰。
- **拖拽打开**：将存档目录拖入主窗口即可导入。
- **复制 / 移动 / 删除**：跨包复制或移动存档时自动按目标平台重命名并迁移备份历史。
- **JSON 编辑器**：直接查看与编辑存档原始 JSON。
- **表格编辑器**：以表格形式编辑关卡记录（`Level_*` 条目）。
- **Meta 编辑器**：编辑 Meta 存档条目。
- **备份历史时间线**：操作前自动备份，按时间线浏览与恢复历史版本。
- **同步诊断**：检测“源文件较新、备份待同步”与“同分组档位冲突”，支持同步到源文件、批量备份、冲突处理。
- **好友号识别**：从 `steam_autocloud.vdf` 提取 `accountid` 作为好友号展示。
- **星级填充**：读取每个存档的 `ScoreStars` 总数并展示。
- **日志与全局异常**：按天文件日志，自动清理过期日志；捕获 AppDomain / UI 线程 / Task 未观察异常。

## 支持的平台与格式

| 平台枚举 | 文件名特征 | 加密 | 说明 |
|---|---|---|---|
| `Oc2Binary` | `*SaveFile_*.save`、`Meta_SaveFile.save` | AES-CBC 加密 | OC2 Steam / Epic 等二进制存档 |
| `AyceJson` | `*SaveFile_*.json`、`Meta_SaveFile.json` | 明文 JSON | AYCE（All You Can Eat）JSON 存档 |
| `XboxBinary` | `*CAMPAIGNSAVE*`、`meta` | AES-CBC 加密 | Xbox 旧版二进制存档 |
| `SwitchJson` | `*CAMPAIGNSAVE*`、`meta` | 明文 JSON | Switch 旧版 JSON 存档 |

平台检测逻辑见 `OvercookedTool.Core/Services/SavePackageService.cs` 的 `DetectPlatform`，文件名解析见 `OvercookedTool.Core/Services/SaveFileNameHelper.cs`。

## 环境要求

### 最终用户

- Windows 操作系统（WinForms 桌面应用）。
- .NET 9 运行时（建议安装 [.NET Desktop Runtime 9.x](https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0)）；从 Release 包直接运行也可，包内已包含所需依赖。

### 开发者

- Windows + [.NET 9 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0)。
- 可选 IDE：Visual Studio 2022、VS Code（C# Dev Kit）或 JetBrains Rider。
- 可选 [Obfuscar](https://github.com/obfuscar/obfuscar) 用于发布版混淆。

## 快速开始

### 用户

1. 从 Release 下载发行包并解压。
2. 运行 `OvercookedTool.App.exe`。
3. 首次启动时会运行 Unity 设备标识 Harness 采集本机标识，完成后即可导入存档包。
4. 点击主界面空白区域或拖入存档目录即可导入。

> 仓库与 Release 包中均不包含任何真实存档样本。

### 开发者

```powershell
# 还原与构建
dotnet build OvercookedTool.sln -c Debug

# 运行测试
dotnet test OvercookedTool.sln

# 运行应用
OvercookedTool.App\bin\Debug\net9.0-windows\OvercookedTool.App.exe
```

详见 [docs/development.md](./docs/development.md)。

## 项目结构

```
胡闹厨房重制/
├─ OvercookedTool.sln                  # 解决方案
├─ Directory.Build.props               # 集中版本号(1.0.0)/产品信息/MIT/语言特性
├─ .editorconfig                       # 代码风格约定
├─ .gitignore
├─ 迁移说明.md                          # Python→C# 迁移历史
├─ readme-for coder.txt                # 给程序员的简短说明
├─ #build-debug.bat                    # Debug 构建脚本
├─ #build-release.bat                  # Release 构建脚本
├─ #create-package.bat                 # 打包脚本（产出 OvercookedSaveTool-Package/）
├─ #配置混淆.bat                        # Obfuscar 混淆配置脚本
├─ docs/
│  ├─ architecture.md                  # 架构设计
│  ├─ development.md                   # 开发指南
│  ├─ usage.md                         # 用户使用手册
│  └─ e2e-testing.md                   # 端到端测试指南
├─ OvercookedTool.Core/                # 核心类库（net9.0）
│  ├─ Crypto/OvercookedCrypto.cs       # AES-CBC + CRC32 加解密
│  ├─ Services/
│  │  ├─ KeyDetector.cs                # 密钥自动探测
│  │  ├─ SavePackageService.cs         # 存档包服务（加载/读写/复制移动/删除/同步/备份/历史）
│  │  ├─ SaveJsonConverter.cs          # OC2↔AYCE 版本转换
│  │  └─ SaveFileNameHelper.cs         # 文件名解析
│  ├─ Models/                          # 数据模型
│  └─ Logging/AppLogger.cs             # 按天日志与清理
├─ OvercookedTool.App/                 # WinForms 应用（net9.0-windows）
│  ├─ Program.cs                       # 入口与全局异常
│  ├─ MainForm.cs / PackageTabView.cs  # 主界面与标签页
│  ├─ JsonEditorForm.cs                # JSON 编辑器
│  ├─ SaveTableEditorForm.cs           # 表格编辑器
│  ├─ MetaTableEditorForm.cs           # Meta 编辑器
│  ├─ SaveTimelineForm.cs              # 历史时间线
│  ├─ SettingsForm.cs / AppSettings.cs # 设置
│  ├─ AboutForm.cs / DonateForm.cs     # 关于与捐赠
│  ├─ UnityHarness/                    # Unity 设备标识采集程序
│  ├─ about_content.json               # 关于页内容
│  ├─ save_display_config.json         # 关卡翻译/显示配置
│  ├─ appsettings.json                 # 应用配置
│  ├─ libcoffee.dll                    # 收款码（改后缀存储）
│  └─ tools.scan_translation_keys.ps1  # 扫描未翻译键脚本
└─ OvercookedTool.Tests/               # xUnit 测试（net9.0）
   ├─ Crypto/                          # 加解密单元测试
   ├─ Services/                        # 服务单元测试
   ├─ Integration/                     # 集成测试（合成 fixtures）
   └─ Helpers/                         # 测试辅助
```

## 构建与打包

构建与打包使用仓库根目录的批处理脚本：

1. `#build-release.bat`：以 Release 配置构建解决方案，输出到 `OvercookedTool.App/bin/Release/`。
2. `#create-package.bat`：将构建产物与依赖整理到 `OvercookedSaveTool-Package/` 目录，作为可分发的发行包。
3. `#配置混淆.bat`：使用 Obfuscar 对 Release 产物进行混淆（可选）。

构建产物默认位于 `OvercookedTool.App\bin\` 下。详见 [docs/development.md](./docs/development.md) 的“打包发布”一节。

## 文档导航

| 文档 | 说明 |
|---|---|
| [docs/architecture.md](./docs/architecture.md) | 整体架构、模块职责、关键流程、加密细节、数据结构 |
| [docs/development.md](./docs/development.md) | 开发环境、构建、测试、格式化、打包、混淆、调试 |
| [docs/usage.md](./docs/usage.md) | 面向最终用户的使用手册 |
| [docs/e2e-testing.md](./docs/e2e-testing.md) | 端到端测试指南（LocalAgent Computer Use） |
| [CONTRIBUTING.md](./CONTRIBUTING.md) | 贡献指南、代码风格、提交规范、测试要求 |
| [CHANGELOG.md](./CHANGELOG.md) | 变更日志 |
| [迁移说明.md](./迁移说明.md) | Python → C# 迁移历史 |

## 安全说明

- **操作前自动备份**：所有写入型操作（编辑、复制覆盖、移动、删除、调整档位、恢复历史、同步、冲突处理）在执行前都会把原文件复制到存档目录下的 `.overcookedtool-backup/`，命名为 `{文件名}.{yyyyMMddHHmmssfff}.{原因}.bak`，按文件保留最近 N 份（默认 10，可在设置中调整）。
- **真实存档目录只读检测**：端到端测试与日常使用中，真实游戏存档目录（如 `%USERPROFILE%\AppData\LocalLow\Team17\...`）仅允许导入检测，不建议直接对其执行保存/同步/删除。
- **建议先复制再操作**：对重要存档，建议先把整个存档包复制到仓库外的临时目录，再用本工具操作，避免误操作。
- **仓库不含真实样本**：真实存档样本已从仓库移除（含个人账户信息），测试改用合成 fixtures。

## 开源协议

本项目基于 [MIT License](./LICENSE) 开源，版权所有 © 2026 OvercookedTool。
