# 贡献指南（Contributing）

感谢你愿意为「胡闹厨房存档管理器（OvercookedTool）」做贡献！本文件说明如何在本地准备好开发环境、提交符合规范的改动，以及如何让测试与文档保持同步。

## 1. 开发环境准备

- 操作系统：Windows（WinForms 桌面应用仅支持 Windows）。
- SDK：[.NET 9 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0)。
- IDE（任选其一）：Visual Studio 2022、VS Code（安装 C# Dev Kit）或 JetBrains Rider。
- 可选：[Obfuscar](https://github.com/obfuscar/obfuscar)，仅用于发布版混淆。
- 克隆仓库后执行 `dotnet restore OvercookedTool.sln` 还原依赖。

## 2. 代码风格

- 遵循仓库根目录的 `.editorconfig`：
  - 缩进 4 空格，CRLF 换行，UTF-8 编码，文件末尾保留换行。
  - C# 公共成员 PascalCase，`private` 字段 `_camelCase`。
  - 大括号独占行（`csharp_new_line_before_open_brace = all`），优先使用花括号。
  - 命名空间使用 file-scoped（`namespace Foo;`）。
- 统一属性集中在 `Directory.Build.props`（版本号、产品信息、语言特性、`zh-CN` 本地化），不要在各 `.csproj` 中重复定义这些属性。
- 可运行以下命令自动格式化：

  ```powershell
  dotnet format OvercookedTool.sln
  ```

## 3. 分支与提交规范

- 分支命名建议：`feat/xxx`、`fix/xxx`、`docs/xxx`、`refactor/xxx`、`chore/xxx`。
- 提交信息建议以前缀开头，便于追溯：

  | 前缀 | 用途 |
  |---|---|
  | `feat:` | 新功能 |
  | `fix:` | 缺陷修复 |
  | `docs:` | 文档变更 |
  | `refactor:` | 不改变行为的重构 |
  | `test:` | 测试相关 |
  | `chore:` | 构建/工具/杂项 |

- 中文 commit message 可接受（本项目以中文为主），示例：`feat: 新增 AYCE 平台识别`、`fix: 修复密钥探测回退顺序`。
- 提交前请确认不要把临时文件、`bin/`、`obj/`、个人 `appsettings.json` 或真实存档样本纳入提交（`.gitignore` 已覆盖常见情况）。

## 4. 测试要求

- **新增功能必须配套测试**：单元测试放在 `OvercookedTool.Tests/` 对应目录（`Crypto/`、`Services/`、`Integration/`）。
- **`dotnet test` 必须通过**：

  ```powershell
  dotnet test OvercookedTool.sln
  ```

- **集成测试用合成 fixtures，不得引入真实存档**：仓库已移除真实存档样本（含个人账户信息）。需要存档数据时，请在测试代码中现场构造合成 JSON / 加密字节，或使用 `OvercookedTool.Tests/Helpers/` 下的辅助工具。
- 真实存档相关测试使用 `SkippableFact`，在缺样本时自动跳过，保证 CI 永远可运行。
- 端到端（WinForms 真实路径）测试遵循 [`docs/e2e-testing.md`](./docs/e2e-testing.md) 的安全边界：只读冒烟优先，写入型测试必须使用独立临时副本，绝不指向真实游戏存档目录。

## 5. 提交前检查清单

在提交 PR 前请逐项确认：

- [ ] `dotnet build OvercookedTool.sln -c Debug` 0 警告 0 错误。
- [ ] `dotnet test OvercookedTool.sln` 全部通过（允许 SkippableFact 跳过）。
- [ ] `dotnet format OvercookedTool.sln` 已执行，无未格式化的改动。
- [ ] 没有引入真实存档样本或个人账户信息。
- [ ] 没有把 `bin/`、`obj/`、`.overcookedtool-backup/`、`logs/` 等产物纳入提交。
- [ ] 若改动了用户可见行为或界面，已同步更新 `docs/usage.md`；若改动了架构/接口，已同步更新 `docs/architecture.md`。
- [ ] 若涉及面向用户的新功能或缺陷修复，已在 [`CHANGELOG.md`](./CHANGELOG.md) 的 `Unreleased` 或对应版本节补充条目。

## 6. 文档更新要求

- 文档统一使用中文 Markdown，与本仓库现有文档保持一致。
- 引用代码时使用相对路径与准确文件名，例如 `OvercookedTool.Core/Services/SavePackageService.cs`。
- 不要在文档中编造截图链接、徽章 URL 或 Release 下载地址；GitHub 仓库地址写作 `https://github.com/Stargazed-Dreamer/OvercookedTool`。
- 不要创建占位图片；如需配图，请提供真实可访问的资源或用 ASCII/表格替代。

## 7. 行为准则

- 保持友善、尊重的交流，针对问题而非个人。
- 不泄露任何真实玩家的账户信息、存档内容或个人数据；真实存档样本不得进入仓库。
- 对存档操作类改动保持谨慎，确保备份机制不被破坏，避免造成用户数据损失。
- 欢迎所有水平的贡献者，对新人提问保持耐心。
