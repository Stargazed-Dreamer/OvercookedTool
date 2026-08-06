# 开发指南（Development）

本文面向开发者，说明如何在本地构建、运行、测试、打包「胡闹厨房存档管理器（OvercookedTool）」，以及调试与项目配置的注意事项。

## 1. 前置依赖

- 操作系统：Windows（WinForms 仅支持 Windows）。
- SDK：[.NET 9 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0)。
- IDE（任选）：Visual Studio 2022、VS Code（C# Dev Kit）或 JetBrains Rider。
- 可选：[Obfuscar](https://github.com/obfuscar/obfuscar)，仅用于发布版代码混淆。
- 可选：LocalAgent（用于端到端测试，见 [`e2e-testing.md`](./e2e-testing.md)）。

## 2. 克隆与还原

```powershell
git clone https://github.com/OvercookedTool/OvercookedTool.git
cd OvercookedTool
dotnet restore OvercookedTool.sln
```

> 仓库不含真实存档样本（已移除以避免泄露个人账户信息），测试依赖合成 fixtures。

## 3. 构建

```powershell
# Debug 构建
dotnet build OvercookedTool.sln -c Debug

# Release 构建
dotnet build OvercookedTool.sln -c Release
```

也可使用仓库根目录的批处理脚本：`#build-debug.bat`（Debug）与 `#build-release.bat`（Release）。

构建产物位于 `OvercookedTool.App\bin\` 下。

## 4. 运行

构建后直接运行可执行文件：

```powershell
OvercookedTool.App\bin\Debug\net9.0-windows\OvercookedTool.App.exe
```

首次启动时，应用会运行 `OvercookedTool.App/UnityHarness/_UnityDeviceUniqueIdentifierHarness.exe` 采集本机 Unity 设备标识（`SystemInfo.deviceUniqueIdentifier`），用于密钥探测候选。采集结果写入 `appsettings.json` 的 `UnityDeviceId` 字段。

## 5. 测试

```powershell
dotnet test OvercookedTool.sln
```

测试项目 `OvercookedTool.Tests`（xUnit）说明：

- **单元测试**：`Crypto/OvercookedCryptoTests.cs`、`Services/KeyDetectorTests.cs`、`Services/SaveFileNameHelperTests.cs`、`Services/SaveJsonConverterTests.cs`。
- **集成测试**：`Integration/RealSavePackageTests.cs`、`Integration/UserSampleTests.cs`。
- **合成 fixtures**：仓库不提供真实存档，集成测试通过现场构造合成 JSON / 加密字节验证端到端流程，保证 CI 始终可运行。
- **SkippableFact**：依赖真实存档样本的用例使用 `SkippableFact`，在样本缺失时自动跳过（不视为失败）。`OvercookedTool.Tests/Helpers/TestSamplePaths.cs` 负责定位可选样本。
- 测试辅助：`OvercookedTool.Tests/Helpers/ByteArrayExtensions.cs`。

期望结果（参考 `docs/e2e-testing.md` 2026-07-31 执行记录）：约 299 项，279 通过、20 跳过、0 失败。

## 6. 代码格式化

```powershell
dotnet format OvercookedTool.sln
```

风格约定集中在根目录 `.editorconfig`：4 空格缩进、CRLF、UTF-8、PascalCase 公共成员、`_camelCase` 私有字段、file-scoped 命名空间、大括号独占行。

## 7. 打包发布

发布流程使用两个批处理脚本，按顺序执行：

1. **构建**：`#build-release.bat`，以 Release 配置构建解决方案，输出到 `OvercookedTool.App/bin/Release/`。
2. **打包**：`#create-package.bat`，将构建产物与依赖（含 `UnityHarness/`、`about_content.json`、`save_display_config.json`、`libcoffee.dll` 等）整理到 `OvercookedSaveTool-Package/` 目录，作为可分发的发行包。

打包产物目录 `OvercookedSaveTool-Package/` 内可直接运行 `OvercookedTool.App.exe`。

## 8. 混淆（Obfuscar）

可选步骤，用于对 Release 产物进行代码混淆：

```powershell
# 配置并执行混淆
#配置混淆.bat
```

混淆配置由 Obfuscar 读取，对 `OvercookedTool.Core` 与 `OvercookedTool.App` 的程序集进行处理。混淆后需重新执行 `#create-package.bat` 整理发行包，并建议手动冒烟验证启动与导入流程。

## 9. 端到端测试

端到端测试基于 LocalAgent Computer Use 验证 WinForms 真实用户路径，**不替代**单元测试与集成测试。完整流程、安全边界与冒烟用例见 [`e2e-testing.md`](./e2e-testing.md)。

要点：

- 只读冒烟优先；写入型测试必须使用独立临时副本，绝不指向真实游戏存档目录。
- 每步操作前重新截图并使用新的 `snapshot_id`，优先 UIA 语义操作，坐标点击兜底。
- 源码构建需 `dotnet test` 与 `dotnet build` 均通过后才视为被测版本。

## 10. 调试技巧

- **日志位置**：`{应用基目录}/logs/overcookedtool-{yyyyMMdd}.log`，按天一个文件。基目录即 `OvercookedTool.App.exe` 所在目录（开发期为 `bin\Debug\net9.0-windows\`）。
- **应用配置**：`OvercookedTool.App/appsettings.json`（运行时由 `AppSettingsStore` 读写），含最近路径、自动检测、日志开关、最近历史条数、备份数、日志保留天数、Unity 设备标识。
- **全局异常**：`Program.cs` 注册了 `AppDomain.UnhandledException`、`Application.ThreadException`、`TaskScheduler.UnobservedTaskException`，未处理异常会写入日志；UI 线程异常还会弹消息框。
- **备份检查**：写操作前会在存档目录的 `.overcookedtool-backup/` 留档，命名 `{文件名}.{yyyyMMddHHmmssfff}.{reason}.bak`，排查数据问题时可对照时间戳与原因标签。
- **密钥排查**：若导入后密钥状态为失败，查看状态栏的“密钥来源”可判断是手动输入、目录名、`steam_autocloud.vdf` 还是回退值；可尝试在标签页手动输入密钥覆盖。

## 11. 项目配置说明

### 11.1 Directory.Build.props

位于仓库根目录，被解决方案下所有项目自动导入，集中定义：

- 产品信息：`Product`、`Authors`、`Copyright`、`PackageLicenseExpression=MIT`、`PackageProjectUrl`。
- 版本号：`Version=1.0.0`、`AssemblyVersion=1.0.0.0`、`FileVersion=1.0.0.0`（`CHANGELOG.md` 与之对齐）。
- 语言与框架特性：`LangVersion=latest`、`Nullable=enable`、`ImplicitUsings=enable`、`TreatWarningsAsErrors=false`。
- 本地化：`NeutralLanguage=zh-CN`。

修改版本号时只需改这一处，并同步更新 `CHANGELOG.md`。

### 11.2 .editorconfig

仓库根目录的 `.editorconfig` 适用于所有文件：源码 4 空格、配置文件 2 空格、Markdown 保留尾随空格（两空格换行）、CRLF 换行、UTF-8 编码。`dotnet format` 与主流 IDE 均识别。

### 11.3 资源文件用途

参考 `readme-for coder.txt`：

| 文件 | 用途 |
|---|---|
| `OvercookedTool.App/about_content.json` | “关于”页面展示内容 |
| `OvercookedTool.App/libcoffee.dll` | 收款码图片改后缀存储（避免被工具链处理） |
| `OvercookedTool.App/save_display_config.json` | 关卡名翻译与显示配置 |
| `OvercookedTool.App/tools.scan_translation_keys.ps1` | 扫描未翻译键的辅助脚本 |
| `OvercookedTool.App/appsettings.json` | 运行时应用配置 |
