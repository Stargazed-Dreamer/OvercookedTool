# 端到端测试指南（LocalAgent Computer Use）

## 1. 目的与范围

本文用于验证 `OvercookedTool.App` 的真实 WinForms 用户路径，而不是替代 `OvercookedTool.Tests` 中的单元测试和集成测试。

默认冒烟范围：

1. 应用可启动并显示主窗口。
2. 设置窗口可打开并取消。
3. 存档目录可通过导入窗口加载。
4. 应用可识别存档版本、密钥、好友号、存档卡片和同步诊断。
5. 选择存档后可打开表格编辑器，再取消退出。
6. 应用可正常关闭，进程退出码为 `0`。

默认不覆盖写入型功能：新建存档、保存编辑、同步到源文件、批量备份、移动、复制和删除。写入型测试必须使用独立的临时副本，且不得指向真实游戏存档目录。

## 2. 测试资产与安全边界

- 推荐样本：仓库中的 `76561198000000002/`，仅用于只读冒烟。
- 真实存档目录（例如 `%USERPROFILE%\\AppData\\LocalLow\\Team17\\...`）只允许检测，不允许执行保存、同步或删除。
- 需要验证写入时，先把一个样本包复制到仓库外的临时目录，记录全部文件的 SHA-256 和修改时间，测试后再次比较。
- Computer Use 操作必须绑定目标 `hwnd`；不要只依赖窗口标题。
- 每次坐标操作前重新截图并使用新的 `snapshot_id`，避免窗口移动后复用旧坐标。
- 优先级为 UIA 语义操作 > OCR 文本定位 > 坐标点击 > 视觉模型兜底。
- 不点击红色“删除”、`同步更改到源文件`、`批量备份待同步项`、`新建存档`、`移动到目标包` 或 `复制到目标包`。
- 测试结束必须关闭应用并释放 LocalAgent 屏幕接管。

## 3. 前置检查

### 3.1 工作区状态

先记录工作区，测试期间不得覆盖用户已有改动：

```powershell
git status --short
```

当前仓库可能存在未提交的核心代码和 `OvercookedTool.Tests/`，E2E 文档与执行不得修改或回滚它们。

### 3.2 自动化测试与构建门禁

```powershell
dotnet test OvercookedTool.sln -c Debug --nologo
dotnet build OvercookedTool.sln -c Debug --nologo
```

只有两个命令都通过时，才把源码构建视为 E2E 被测版本。正常启动命令：

```powershell
OvercookedTool.App\bin\Debug\net9.0-windows\OvercookedTool.App.exe
```

若源码构建被已知问题阻塞，可以用下面的已打包程序完成“现有发布物冒烟”，但报告必须明确写成发布物验证，不能声称源码构建通过：

```powershell
OvercookedSaveTool-Package\OvercookedTool.App.exe
```

### 3.3 LocalAgent 状态

1. 调用 `agent_guide` 获取当前任务指引。
2. 阅读 LocalAgent 的 `.agents/skills/computer_use.md`。
3. 检查 `/health`：`status=ok`、`screen.capture_available=true`、`screen.admin_privileges=true`、`screen.uia.available=true`。
4. 调用 `screen_request_control`，任务描述必须说明“只读导入、打开后取消编辑器、不保存或删除”。
5. 用 `screen_snapshot` 或 `list_windows` 记录当前窗口，避免同名窗口误匹配。

## 4. 推荐的 MCP 操作模式

### 4.1 每一步的固定闭环

1. `capture_screen(mode="window", hwnd=..., format="inline")` 截图并取得 `snapshot_id`。
2. `screen_accessibility_snapshot(hwnd=...)` 获取 UIA 树。
3. 优先使用 `screen_semantic_action` 的 `invoke`、`set_value`、`toggle`。
4. 自绘存档卡片没有稳定 UIA 名称时，才按截图计算屏幕物理坐标，并调用 `execute_action`；必须同时传 `hwnd` 和最新 `snapshot_id`。
5. 操作后重新截图或读取 UIA/OCR，验证后再进入下一步。
6. 同一步失败三次即停止，不复用旧坐标盲点。

### 4.2 WinForms 模态窗口注意事项

本项目的菜单和按钮会通过 `ShowDialog` 打开模态窗口。LocalAgent 的 UIA `invoke` 或坐标 `click` 请求可能一直等到模态窗口关闭，返回端甚至可能出现 COM 异常，但界面实际上已经成功打开。

处理规则：

1. 发起打开模态窗口的动作后，先用另一次 `screen_snapshot` 确认子窗口已经出现。
2. 不要在第一个动作尚未返回时继续排队坐标操作，否则旧 `hwnd` 关闭后可能发生焦点漂移。
3. 子窗口内的输入优先用 `screen_accessibility_snapshot` + `screen_semantic_action(action="set_value")`。
4. 只读测试使用“取消”或 `screen_window_close` 退出子窗口。
5. 以截图和子窗口生命周期作为业务结果，不能只看打开动作的返回状态。

## 5. 冒烟用例

### E2E-01 启动与首屏

1. 启动被测 EXE，等待标题为“胡闹厨房存档管理器”的窗口。
2. 若首启出现“本机 Unity 设备标识”和 Unity Harness，等待工具完成；不手工填写猜测值。
3. 截取主窗口并获取 UIA 树。

通过条件：

- 主窗口可见且未最小化。
- 菜单包含“文件”“设置”“关于”。
- `+` 标签被选中。
- 空白区显示“点击此空白区域导入存档包”。

### E2E-02 设置窗口只读检查

1. 通过 UIA 或菜单点击“设置”。
2. 验证窗口包含自动检测、日志、最近历史条数、备份数、“保存”和“取消”。
3. 不改变控件，使用“取消”或关闭按钮退出。

通过条件：设置窗口可打开、控件完整、取消后回到主窗口，主窗口仍可操作。

### E2E-03 导入 OC2 样本

1. 点击主界面空白导入区，等待“导入存档包”窗口。
2. 获取新窗口的 UIA 快照，找到角色为 `Edit` 的路径输入框。
3. 用 `set_value` 写入样本目录绝对路径，并使用 `uia_value_equals` 验证回读值。
4. 重新获取 UIA 快照，调用“导入”按钮。
5. 等待导入窗口消失，重新截取主窗口。

通过条件：

- 新标签名称为样本目录名。
- 路径框与输入目录一致。
- 显示“存档包版本: Oc2”。
- 密钥状态为成功，密钥来源可由状态栏确认。
- 能看到 Meta、主线和 DLC 存档卡片。
- “冲突”区域显示未发现冲突；没有错误弹窗。

“待同步”中的“未发现工具备份”是诊断信息，不代表导入失败。

### E2E-04 选择存档并打开编辑器

1. 截取主窗口。存档卡片为自绘控件时，计算第一张“档位 1”卡片中心的屏幕物理坐标。
2. 使用最新 `snapshot_id` 和主窗口 `hwnd` 单击卡片。
3. 重新截图，验证卡片高亮、详情显示 `CoopSlot_SaveFile_0.save`，底部“编辑存档”按钮启用。
4. 打开“编辑存档”。
5. 验证编辑器标题、版本 `Oc2`、分组 `2代主线 (CoopSlot)`、关卡记录表、“取消”和“保存”。
6. 不修改单元格，不点“保存”，使用“取消”或关闭按钮退出。
7. 回到主窗口后重新截图。

通过条件：编辑器能显示关卡表；退出后没有黄色待编辑状态，没有出现“同步更改到源文件”的待写回草稿。

### E2E-05 收尾

1. 正常关闭主窗口。
2. 等待启动终端结束并检查退出码。
3. 调用 `screen_release_control`。
4. 再次执行 `git status --short`，与测试前对比。
5. 检查样本文件修改时间或 SHA-256 未变化。

通过条件：进程退出码为 `0`；样本未被写入；Git 只出现预期的测试文档改动。

## 6. 证据清单

每次执行至少保留以下信息：

- 日期、被测 EXE 路径、Git 提交或工作区状态。
- `dotnet test` 与 `dotnet build` 的退出码和关键错误。
- LocalAgent 版本、管理员权限与 UIA 可用性。
- 首屏、导入成功、编辑器、取消后主窗口四个截图检查点。
- 导入后的版本、密钥状态、好友号、冲突和待同步摘要。
- 应用退出码、测试后 Git 状态、样本修改时间或哈希。

## 7. 2026-07-31 实际执行记录

被测发布物：`OvercookedSaveTool-Package/OvercookedTool.App.exe`

| 检查项 | 结果 | 证据/说明 |
|---|---|---|
| LocalAgent 后端 | 通过 | `0.15.0`，`capture_available=true`，管理员权限与 UIA 均可用 |
| 自动化测试/源码构建 | 通过 | 同日移除 `OvercookedTool.App.csproj` 文件头的 4 个连续 UTF-8 BOM 后，`dotnet test` 共 299 项：279 通过、20 跳过、0 失败；`dotnet build` 为 0 警告、0 错误 |
| 发布物启动 | 通过 | 主窗口出现，首启 Unity Harness 完成后状态栏显示设备标识已保存 |
| 设置窗口 | 通过 | 自动检测、日志、历史条数、备份数控件可见；未修改并关闭 |
| 导入样本 | 通过 | 加载 `76561198000000002/`，识别 `Oc2`、目录名密钥有效、好友号 `39734274` |
| 存档与诊断 | 通过 | Meta、主线和 DLC 卡片可见；未发现冲突；待同步仅报告尚无工具备份 |
| 编辑器 | 通过 | `CoopSlot_SaveFile_0.save` 表格编辑器显示 52 条关卡记录；未修改、未保存并关闭 |
| 安全收尾 | 通过 | 样本 `CoopSlot_SaveFile_0.save` 修改时间仍为 `2026-07-01 01:12:52`；应用退出码 `0`；E2E 操作未写入源码或样本，后续仅修复项目文件 BOM |

执行期间发布物目录中的忽略文件 `OvercookedSaveTool-Package/appsettings.json` 被首启/最近路径逻辑更新，这是应用配置副作用，不是存档写入。

## 8. 当前已知问题

1. UIA 对自绘存档卡片只暴露无名称的 `Pane`，卡片选择仍需要截图坐标兜底。
2. LocalAgent 对打开 WinForms 模态窗口的 `invoke/click` 可能延迟返回或返回 COM 异常，必须以子窗口截图和生命周期验证为准。
3. `screen_request_control` 的返回状态与后续坐标动作是否再次等待接管确认可能不一致；坐标操作前仍应检查前台窗口，发现焦点漂移立即停止。
