# FerrisPulse 在线检查更新、黑金专属关于页、全自动原地热替换与闭源混淆防护实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 保持主界面紧凑原生，打造黑金专属关于与致谢卡片、在线检查更新、流光下载弹窗、无脑原地热替换与编译期闭源防反编译混淆加固的完整闭环。

**Architecture:** 
1. **纯粹原生主界面**: 顶栏右上角增设极具质感的 `[ ℹ️ ]` 关于与致谢图标按钮，主界面不增加笨重设置卡片，外设参数仍按设备上下文在主界面就地调节。
2. **专属黑金关于与致谢卡片 (`AboutForm`)**: 展示绿色呼吸发光 Logo、`FerrisPulse · 灵脉`、版本徽章、作者署名 `Ferris-echo`、在线检查更新控制行、微信/支付宝赞赏码（智能自适应加载 `donate.png`）。
3. **云端探测与原子置换 (`UpdateManager` & `UpdateNoticeForm`)**: GitHub Release-Only 仓库 + 国内双镜像（`ghproxy.net` / `mirror.ghproxy.com`）秒级探测；新版弹窗展示更新日志与 `ModernProgressBar` 流光进度；后台隐藏 PowerShell 命令等待旧进程退出后原子覆盖置换并拉起新版。
4. **编译期闭源加固**: `build.bat` 构建流水线集成 Obfuscar，符号重命名、敏感字符串加密、控制流混淆，彻底防御 ILSpy/dnSpy 逆向。

**Tech Stack:** C# 5.0 (.NET Framework 4.0), Windows Forms, Win32 API, PowerShell, Obfuscar 2.2+

**Spec:** [docs/superpowers/specs/2026-10-02-online-update-and-obfuscation-design.md](file:///d:/%E7%94%B5%E9%87%8F%E7%AE%A1%E5%AE%B6/docs/superpowers/specs/2026-10-02-online-update-and-obfuscation-design.md)

## Global Constraints
- C# 5.0 / .NET Framework 4.0 铁律：严禁使用 C# 6+ 语法。
- 所有新增与修改文件必须严格保存为【UTF-8 with BOM】编码（使用 `tools\ensure_bom.ps1` 校验）。
- 零干扰原则：自动静默检测绝不在网络失败时弹出错误对话框；开机自启动静默检测延迟 10 秒发起，不争抢启动资源。
- 视觉统一规范：窗体与控件 100% 复用 `ModernThemeHelper`、`ModernButton`、`ModernProgressBar`。
- 零外部运行时 DLL 依赖：主程序保持单文件便携绿色运行。

## Review Focus
1. **网络超时与镜像不可达容灾**：当主镜像由于网络波动超时（>3s）或返回 HTTP 5xx 时，客户端必须无缝顺延尝试备用镜像与直连，不可发生未捕获 WebException 崩溃。
2. **下载文件完整性与篡改防御**：当下载中断或 SHA256 哈希校验不匹配时，必须立即清理临时文件 `.update` 并向用户明确报警，绝不可进入热替换自毁流程。
3. **Windows 进程退出竞争 (Race Condition)**：伴随更新命令在等待旧进程退出时，若超时 10 秒进程仍未退出，必须安全中止，避免出现文件写冲突。
4. **混淆后 Windows Forms 入口与反射兼容性**：Obfuscar 混淆规则必须保留 `Program` 主入口及 Win32 事件回调签名，确保混淆后的 exe 正常运行且事件不失效。
5. **赞赏二维码不存在时的优雅占位**：当用户目录未放置 `donate.png` 时，`AboutForm` 必须渲染高质感暗黑占位卡片与说明文字，不可发生 FileNotFound 异常或空白崩溃。

---

### Task 1: 云端模型与配置项扩展 (`src/Models/UpdateInfo.cs`, `src/Core/ConfigManager.cs`)

**Files:**
- Create: `src/Models/UpdateInfo.cs`
- Modify: `src/Core/ConfigManager.cs`
- Test: `tests/test_update_config_policy.ps1`

**Interfaces:**
- Consumes: `ConfigManager.GetInt`, `ConfigManager.SetInt`, `ConfigManager.GetString`, `ConfigManager.SetString`
- Produces: 
  - `UpdateInfo` 类：`LatestVersion`, `ReleaseDate`, `MinSupportedVersion`, `DownloadUrl`, `Sha256`, `FileSize`, `Changelog`
  - `ConfigManager.AutoCheckUpdate`: `GetAutoCheckUpdate() -> bool`, `SetAutoCheckUpdate(bool enabled)`
  - `ConfigManager.LastUpdateCheckTime`: `GetLastUpdateCheckTime() -> string`, `SetLastUpdateCheckTime(string timeStr)`

- [ ] **Step 1: 编写失败测试用例 `tests/test_update_config_policy.ps1`**
  编写测试验证 `ConfigManager` 的 `AutoCheckUpdate` 读写持久化（默认值为 1/true）、`LastUpdateCheckTime` 读写，以及 `UpdateInfo` 类的结构实例化。

- [ ] **Step 2: 运行测试验证失败 (RED)**
  运行: `powershell -NoProfile -ExecutionPolicy Bypass -File tests/test_update_config_policy.ps1`
  预期: FAIL。

- [ ] **Step 3: 创建 `src/Models/UpdateInfo.cs` 并扩展 `src/Core/ConfigManager.cs` (GREEN)**
  - 在 `src/Models/UpdateInfo.cs` 中实现 `UpdateInfo` 数据实体类（UTF-8 with BOM，包含 List<string> Changelog）。
  - 在 `src/Core/ConfigManager.cs` 中增加配置常量 `AutoCheckUpdate = "AutoCheckUpdate"`、`LastUpdateCheckTime = "LastUpdateCheckTime"` 及便捷读写方法。

- [ ] **Step 4: 运行测试验证通过 (GREEN)**
  运行: `powershell -NoProfile -ExecutionPolicy Bypass -File tests/test_update_config_policy.ps1`
  预期: PASS。

- [ ] **Step 5: 编码自检与 BOM 确认**
  运行: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/ensure_bom.ps1 -Path src/Models/UpdateInfo.cs`

---

### Task 2: 版本比对与容灾网络更新探测服务 (`src/Services/UpdateManager.cs`)

**Files:**
- Create: `src/Services/UpdateManager.cs`
- Test: `tests/test_update_service.ps1`

**Interfaces:**
- Consumes: `UpdateInfo`, `ConfigManager`
- Produces:
  - `UpdateManager.CompareVersions(string v1, string v2) -> int` (SemVer 比对)
  - `UpdateManager.IsNewerVersion(string remoteVersion, string localVersion) -> bool`
  - `UpdateManager.ParseVersionJson(string jsonText) -> UpdateInfo` (零外部依赖 JSON 解析器)
  - `UpdateManager.CheckUpdateAsync(Action<bool, UpdateInfo, string> callback)` (多镜像轮询异步探测)
  - `UpdateManager.DownloadFileAsync(string url, string targetPath, Action<long, long, double> progressCallback, Action<bool, string> completedCallback)`
  - `UpdateManager.ComputeFileSha256(string filePath) -> string`

- [ ] **Step 1: 编写失败测试用例 `tests/test_update_service.ps1`**
  编写测试验证 SemVer 两段式与三段式比对、JSON 解析能力、SHA256 校验逻辑。

- [ ] **Step 2: 运行测试验证失败 (RED)**
  运行: `powershell -NoProfile -ExecutionPolicy Bypass -File tests/test_update_service.ps1`
  预期: FAIL。

- [ ] **Step 3: 编写 `src/Services/UpdateManager.cs` (GREEN)**
  - 实现健壮的 `CompareVersions` 与 `IsNewerVersion`。
  - 实现内置轻量零依赖 `ParseVersionJson`。
  - 实现双镜像容灾探测（`ghproxy.net` -> `mirror.ghproxy.com` -> 直连，超时 3000ms）。
  - 实现流式带速率计算的下载引擎 `DownloadFileAsync` 与 `ComputeFileSha256`。

- [ ] **Step 4: 运行测试验证通过 (GREEN)**
  运行: `powershell -NoProfile -ExecutionPolicy Bypass -File tests/test_update_service.ps1`
  预期: PASS。

- [ ] **Step 5: 编码自检与 BOM 确认**
  运行: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/ensure_bom.ps1 -Path src/Services/UpdateManager.cs`

---

### Task 3: 专属黑金关于与致谢卡片 (`src/UI/Forms/AboutForm.cs`)

**Files:**
- Create: `src/UI/Forms/AboutForm.cs`
- Test: `tests/test_about_form_presentation.ps1`

**Interfaces:**
- Consumes: `ModernThemeHelper`, `ModernButton`, `ModernCheckBox`, `UpdateManager`, `ConfigManager`
- Produces:
  - `AboutForm()` 专属黑金卡片窗口
  - 自动渲染软件 Logo、版本徽章、作者署名 `Ferris-echo`、更新控制与赞赏二维码

- [ ] **Step 1: 编写 UI 渲染测试 `tests/test_about_form_presentation.ps1`**
  编写测试验证 `AboutForm` 尺寸（460×520）、无边框圆角、暗黑背景（`#121212`）、包含作者署名 `Ferris-echo`、检查更新按钮、赞赏卡片与安全加载逻辑。

- [ ] **Step 2: 运行测试验证失败 (RED)**
  运行: `powershell -NoProfile -ExecutionPolicy Bypass -File tests/test_about_form_presentation.ps1`
  预期: FAIL（`AboutForm` 类不存在）。

- [ ] **Step 3: 实现 `src/UI/Forms/AboutForm.cs` (GREEN)**
  - 顶部绿色发光 Logo + `FerrisPulse · 灵脉` + 绿色高光胶囊徽章。
  - 优雅作者署名 `由 Ferris-echo 独立设计与开发`。
  - 在线更新控制行：当前版本状态、`[ 立即检查更新 ]` 按钮、`☑ 自动检查更新` 开关。
  - 赞赏支持卡片：圆角暗黑容器，自动检测并高质量呈现 `donate.png` 或 `qr.png`，无图时展示优雅暗黑占位卡片。

- [ ] **Step 4: 运行测试验证通过 (GREEN)**
  运行: `powershell -NoProfile -ExecutionPolicy Bypass -File tests/test_about_form_presentation.ps1`
  预期: PASS。

- [ ] **Step 5: 编码自检与 BOM 确认**
  运行: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/ensure_bom.ps1 -Path src/UI/Forms/AboutForm.cs`

---

### Task 4: 升级通知与流光下载弹窗 (`src/UI/Forms/UpdateNoticeForm.cs`)

**Files:**
- Create: `src/UI/Forms/UpdateNoticeForm.cs`
- Test: `tests/test_update_ui_presentation.ps1`

**Interfaces:**
- Consumes: `ModernThemeHelper`, `ModernButton`, `ModernProgressBar`, `UpdateInfo`, `UpdateManager`
- Produces:
  - `UpdateNoticeForm(UpdateInfo updateInfo, string currentVersion)` 对话框

- [ ] **Step 1: 编写新版本通知弹窗契约测试 `tests/test_update_ui_presentation.ps1`**
  编写测试验证 `UpdateNoticeForm` 尺寸（460×420）、无边框圆角、新版本徽章、暗黑日志滚动卡片、稍后提醒与立即升级按钮。

- [ ] **Step 2: 运行测试验证失败 (RED)**
  运行: `powershell -NoProfile -ExecutionPolicy Bypass -File tests/test_update_ui_presentation.ps1`
  预期: FAIL。

- [ ] **Step 3: 实现 `src/UI/Forms/UpdateNoticeForm.cs` (GREEN)**
  - 无边框暗黑窗口，支持顶部拖拽。
  - 绿色发光胶囊徽章展示新版本。
  - 暗黑日志卡片容器，支持鼠标滚轮平滑滑动。
  - 点击“立即升级”后平滑展开 `ModernProgressBar`，流光绿渐变，显示实时下载百分比与网速。
  - 下载完成后提示“准备重启更新...”，触发原子热替换。

- [ ] **Step 4: 运行测试验证通过 (GREEN)**
  运行: `powershell -NoProfile -ExecutionPolicy Bypass -File tests/test_update_ui_presentation.ps1`
  预期: PASS。

- [ ] **Step 5: 编码自检与 BOM 确认**
  运行: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/ensure_bom.ps1 -Path src/UI/Forms/UpdateNoticeForm.cs`

---

### Task 5: 原子原地自替换伴随脚本与生命周期联动 (热替换、顶栏入口、托盘与静默检测)

**Files:**
- Modify: `src/Services/UpdateManager.cs`
- Modify: `src/UI/Forms/MainForm.cs`
- Modify: `src/UI/Forms/MainForm.UI.cs`
- Modify: `src/UI/Forms/MainForm.Tray.cs`
- Modify: `src/Core/Program.cs`
- Test: `tests/test_in_place_self_update.ps1`

**Interfaces:**
- Consumes: `UpdateManager.LaunchSelfReplace(string tempFilePath)`, `AboutForm`, `UpdateNoticeForm`
- Produces:
  - 主界面顶栏右上角 `[ ℹ️ ]` 关于图标按钮
  - 托盘右键菜单“关于 FerrisPulse”与“检查更新”纯净项
  - 开机 10 秒后静默探测（若开启），发现新版时直接唤起 UpdateNoticeForm 升级弹窗

- [ ] **Step 1: 编写原子替换与生命周期测试 `tests/test_in_place_self_update.ps1`**
  编写测试验证替换命令生成、主界面顶栏按钮挂接、托盘菜单新增项与类别 B 收起判定。

- [ ] **Step 2: 运行测试验证失败 (RED)**
  运行: `powershell -NoProfile -ExecutionPolicy Bypass -File tests/test_in_place_self_update.ps1`
  预期: FAIL。

- [ ] **Step 3: 实施原子替换引擎与 UI/生命周期挂接 (GREEN)**
  - 在 `UpdateManager` 中实现 `LaunchSelfReplace`：生成后台隐藏 PowerShell 命令，等待旧 PID 退出后原子覆盖并启动新程序，随后调用 `Application.Exit()`。
  - 在 `MainForm.UI.cs` 顶栏右上角添加 `btnTopAbout` (`[ ℹ️ ]`)，点击弹出 `AboutForm`。
  - 在 `MainForm.Tray.cs` 中增加“关于 FerrisPulse”与“检查更新”纯净化项。
  - 在 `Program.cs` / `MainForm.cs` 中挂载开机延迟 10 秒静默更新探测。

- [ ] **Step 4: 运行测试验证通过 (GREEN)**
  运行: `powershell -NoProfile -ExecutionPolicy Bypass -File tests/test_in_place_self_update.ps1`
  预期: PASS。

- [ ] **Step 5: 编码自检与 BOM 确认**
  运行 `tools/ensure_bom.ps1` 校验所有修改文件。

---

### Task 6: 编译流水线集成代码混淆保护与加固验证 (`tools/obfuscar/`, `build.bat`)

**Files:**
- Create: `tools/obfuscar/obfuscar.xml`
- Create: `tools/setup_obfuscator.ps1`
- Modify: `build.bat`
- Test: `tests/test_obfuscation_integrity.ps1`

**Interfaces:**
- Consumes: CSC 编译生成的 `FerrisPulse.raw.exe`
- Produces: 混淆加固后的正式发布版 `FerrisPulse.exe`

- [ ] **Step 1: 编写混淆加固验证测试 `tests/test_obfuscation_integrity.ps1`**
  编写测试验证混淆器下载就绪、符号混淆与二进制可运行性。

- [ ] **Step 2: 运行测试验证失败 (RED)**
  运行: `powershell -NoProfile -ExecutionPolicy Bypass -File tests/test_obfuscation_integrity.ps1`
  预期: FAIL。

- [ ] **Step 3: 创建混淆配置与升级构建流水线 (GREEN)**
  - 创建 `tools/setup_obfuscator.ps1`。
  - 创建 `tools/obfuscar/obfuscar.xml`，配置符号隐藏、属性重命名、私有成员重命名、字符串加密与控制流保护，保留 `FerrisPulse.Program`。
  - 更新 `build.bat`：编译 -> Obfuscar 混淆 -> 产出最终 `FerrisPulse.exe`。

- [ ] **Step 4: 运行测试验证通过 (GREEN)**
  运行: `powershell -NoProfile -ExecutionPolicy Bypass -File tests/test_obfuscation_integrity.ps1`
  预期: PASS。

---

### Task 7: 全量回归测试、版本发布与实机运行验证 (`tools/run_all_tests.ps1`, `version.json`, Release Packaging)

**Files:**
- Create: `version.json` (云端发布模板文件)
- Modify: `tools/run_all_tests.ps1`

- [ ] **Step 1: 创建 `version.json` 规范模板**
- [ ] **Step 2: 将新测试注册到 `tools/run_all_tests.ps1`**
- [ ] **Step 3: 执行全套自动化回归测试套件 (确保全部测试 100% 全绿)**
- [ ] **Step 4: 执行全量混淆编译与 Release 打包**
- [ ] **Step 5: 恢复实机托盘运行环境并验证进程**
