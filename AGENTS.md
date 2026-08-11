# 维护者守则（AGENTS.md）

本文件面向「胡闹厨房存档管理器（OvercookedTool）」的维护者与 AI 协作智能体，规定日常维护中**必须遵守**的隐私要求。它与 [`CONTRIBUTING.md`](./CONTRIBUTING.md)、[`docs/development.md`](./docs/development.md)、[`docs/architecture.md`](./docs/architecture.md) 互补：前者讲流程与风格，本文件讲"红线"。

> 触发条件：任何对源码、配置、构建脚本、CI、文档的修改，都必须先对照本文件第 2 节自查。

---

## 1. 项目定位速览

本项目是 **Windows 专有**的桌面工具，基于 .NET 9 WinForms，不追求跨平台。

| 项目 | TFM | 定位 | 维护原则 |
|---|---|---|---|
| `OvercookedTool.Core` | `net9.0` | 核心类库 | 业务逻辑放此处；本身可跨平台构建，但不要为跨平台而迁就 |
| `OvercookedTool.Tests` | `net9.0` | 测试项目 | 合成 fixtures 优先，真实样本用 `SkippableFact` |
| `OvercookedTool.App` | `net9.0-windows` + WinForms | Windows 桌面应用 | UI 与平台相关代码放此处；新增功能尽量下沉到 Core |
| `temp/*` | `net9.0` | 诊断工具 | 一次性脚本，可硬编码本地路径但**禁止入库**（已被 `.gitignore` 排除） |

**Windows 专有说明**：App 层使用 WinForms（`net9.0-windows`），这是有意的设计决策。新增功能优先下沉到 Core，App 仅做 UI 适配。若未来要跨平台，需整体迁移到 Avalonia / Eto.Forms / MAUI，不在当前维护范围内。

---

## 2. 隐私红线（必须）

本项目已公开发布，任何个人隐私泄露都会永久留在 git 历史中。下列规则**没有例外**。

### 2.1 禁止入库的内容

- ❌ 真实玩家存档（含个人 SteamID64 命名的目录、`steam_autocloud.vdf`、任何 `.save` / `.json` 真实存档）
- ❌ 真实姓名、手机号、个人邮箱、个人 QQ/微信号、个人支付账户信息
- ❌ 包含 `%USERPROFILE%\` 或任何个人盘符路径的绝对路径
- ❌ `.env`、API key、token、数据库连接串、SSH key
- ❌ `.lnk` 快捷方式、`.user` 用户配置、IDE 个人配置
- ❌ 收款码、个人二维码（如需公开请走 release 资产，不入仓库源码）

### 2.2 必须脱敏的测试数据

- 测试代码中的 SteamID64 / accountid **必须使用明显虚假的值**（如 `76561198000000001`、`76561198000000002`、`accountid = "39734273"`），不得使用开发者个人账号的真实 ID
- 测试辅助（`OvercookedTool.Tests/Helpers/`）如需引用本地真实样本，路径常量可保留但**必须**：
  - 用 `SkippableFact` / `SkippableTheory` 包裹，样本缺失自动跳过
  - 添加注释说明"仅供本地 `参考/` 目录测试，CI 会跳过"
  - 真实样本目录 `参考/` 已被 `.gitignore` 排除，**永远不要**解除该排除
- `SkippableTheory` 的 `MemberData` 在样本缺失时必须返回占位数据（如 `new object[] { "__SKIP__" }`），不能直接 `yield break`，否则 xUnit 会报错"expected 1 parameter value, but 0 parameter values were provided"

### 2.3 git 历史意识

- 一旦敏感信息进入 git，仅删除当前版本**无效**，必须用 `git filter-repo` 清理历史（含 `--replace-text` 清理字符串残留），已 clone 用户需重新 clone
- 提交前用 `git diff --cached` 自查；不要用 `git add .` / `git add -A`，按文件名显式添加
- commit message 不得包含个人调试信息（如"问 xxx 确认"、"群友报错"等非正式表述）
- author 统一为 `Stargazed-Dreamer <101247328+Stargazed-Dreamer@users.noreply.github.com>`，不混用其他账号

### 2.4 个人路径清理清单（新增文件时复查）

新增文件时若包含以下模式，**必须**改为相对/参数化：

- 硬编码盘符路径：`%USERPROFILE%\`、`<盘符>:\<个人项目路径>\`
- 个人项目结构：`<盘符>:\path\to\project\...`
- 个人 VS/NuGet 缓存：`<盘符>:\path\to\cache\...`、`%USERPROFILE%\.nuget\...`
- 临时调试路径：`<仓库根>\参考\我的存档\...`（temp 工具内可临时使用，禁止入库）

### 2.5 已处理项（历史记录）

> 以下问题在公开化工作中已全部处理，保留记录供后续参考：

- ✅ `OvercookedTool.App/tools.scan_translation_keys.ps1:2` — 默认参数已改为 `[string]$Root = $PSScriptRoot`
- ✅ `OvercookedTool.App/about_content.json` — `github_url` 已统一为 `https://github.com/Stargazed-Dreamer/OvercookedTool`
- ✅ `OvercookedTool.App/OvercookedTool.App.csproj.user` — 已 `git rm --cached` 取消跟踪
- ✅ `release - 快捷方式.lnk`、`obfuscar - 快捷方式.lnk` — 已删除，`.gitignore` 已加 `*.lnk` 规则
- ✅ `OvercookedTool.App/libcoffee.dll`（收款码）、`coffee.png`、`#配置混淆.bat`（Obfuscar 入口） — 已 `git rm --cached` 取消跟踪（磁盘保留），`.gitignore` 已加排除规则，git 历史已清理
- ✅ 测试代码中所有个人 SteamID64 — 已替换为虚假值 `76561198000000001` / `76561198000000002`
- ✅ git 历史中残留的真实存档目录（以真实 SteamID64 命名）与 `obj/` — 已用 `git filter-repo` 清理（含 `--replace-text` 清理字符串残留）
- ✅ git 历史中所有 commit message — 已重写为规范格式（`feat/fix/docs/chore/test/style` 前缀）

---

## 3. AI 协作智能体附加约束

若维护者是 AI 智能体（含本仓库的 LocalAgent Computer Use 端到端测试），额外遵守：

1. **不点击写入型按钮**：删除、同步、批量备份、新建、移动、复制等写入型操作在端到端测试中**禁用**，只读冒烟优先。完整安全边界见 [`docs/e2e-testing.md`](./docs/e2e-testing.md)。
2. **不修改 `.gitignore` 的隐私排除规则**：`参考/`、`temp/`、SteamID64 目录排除规则不得删除或弱化。
3. **不重写 git 历史**：除非用户显式要求清理隐私，否则不得执行 `git filter-repo` / `git push --force` / `git rebase` 等历史重写操作。
4. **不自动提交真实样本**：若在 `参考/` 目录发现真实存档，禁止 `git add` 该目录任何文件。
5. **修改前先读**：对任何文件做修改前，必须先用 Read 工具读取目标文件，理解上下文后再改。禁止凭文件名猜测内容直接编辑。
6. **测试数据脱敏**：自动生成测试用例时，SteamID64 / accountid 必须用明显虚假值（`76561198000000000` 起），禁止使用从 `参考/` 真实样本中读到的 ID。

---

## 4. 维护前自查清单

每次提交前逐项确认：

### 隐私
- [ ] 没有引入真实存档、个人 SteamID64、个人账户信息
- [ ] 测试数据使用明显虚假的 ID（`76561198000000000` 起）
- [ ] 没有硬编码个人盘符路径（`%USERPROFILE%\`、`<盘符>:\<项目路径>\` 等）
- [ ] 没有 `.lnk`、`.user`、`.env`、API key 入库
- [ ] commit message 不含个人调试信息
- [ ] author 为 `Stargazed-Dreamer`，不混用其他账号

### Windows 专有
- [ ] App 层改动未引入非 Windows 依赖（App 本就是 `net9.0-windows`）
- [ ] Core 层改动未破坏构建（Core 是 `net9.0`，可跨平台构建，但不需要为跨平台而迁就）
- [ ] 新增 `Process.Start` 调用未硬编码非 Windows 命令（App 层可用 `explorer.exe` 等 Windows 命令）

### 文档同步
- [ ] 改动用户可见行为 → 更新 [`docs/usage.md`](./docs/usage.md)
- [ ] 改动架构/接口 → 更新 [`docs/architecture.md`](./docs/architecture.md)
- [ ] 新增功能/缺陷修复 → 更新 [`CHANGELOG.md`](./CHANGELOG.md) `Unreleased` 节
- [ ] 改动涉及本文件红线 → 同步更新本文件

---

## 5. 参考文档

- [`CONTRIBUTING.md`](./CONTRIBUTING.md) — 贡献流程、代码风格、提交规范
- [`docs/development.md`](./docs/development.md) — 本地构建、运行、测试、打包指南
- [`docs/architecture.md`](./docs/architecture.md) — 三层架构、模块职责、加密细节、关键流程
- [`docs/usage.md`](./docs/usage.md) — 用户使用手册
- [`docs/e2e-testing.md`](./docs/e2e-testing.md) — 端到端测试流程与 AI 操作安全边界
- [`迁移说明.md`](./迁移说明.md) — 从 Python+C# 混合迁移到纯 C# .NET 9 的记录
