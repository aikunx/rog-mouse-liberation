# FerrisPulse 在线检查更新、全自动原地热替换与闭源混淆防护架构设计规范

**文档版本**: 1.0.0  
**日期**: 2026-10-02  
**状态**: 待评审 (Ready for Review)  
**作者**: Antigravity & Ferris-echo  

---

## 1. 目标与设计原则 (Objectives & Principles)

### 1.1 背景与目标
FerrisPulse 作为专业的游戏外设电量与硬件管理工具，已迭代至 v4.2.0 旗舰版。为了在**无服务器预算**的前提下，让用户享受一键自动更新新版，同时**彻底保护作者知识产权，避免核心源码被反编译逆向**，本规范制定了一套零成本云端分发、无脑原地热替换、现代暗黑视觉统一与编译期全自动代码混淆的完整闭环方案。

### 1.2 核心设计原则
1. **零成本与高可用 (Zero Cost & High Availability)**：利用 GitHub 纯分发仓库（Release-Only）托管元数据与安装包，结合国内免费高速代理镜像（`ghproxy.net` / `mirror.ghproxy.com`），确保国内网络环境下秒级探测与高速下载。
2. **100% 闭源安全 (Closed-Source Assurance)**：
   - 云端公开仓库**仅托管 `version.json` 与发布安装包**，严禁上传任何源代码工程。
   - 编译构建流水线集成 **Obfuscar** 进行符号重命名、敏感字符串加密和控制流平坦化，彻底粉碎 ILSpy / dnSpy 等反编译工具。
3. **无脑原地热替换 (In-Place Self-Updating)**：绕过 Windows 运行时文件锁限制，下载完成后由毫秒级伴随脚本自动完成“进程退出 -> 文件覆盖 -> 拉起新版”，用户无需手动解压、复制或重命名。
4. **统一电竞暗黑美学 (Unified Dark Aesthetic)**：新增的更新检测弹窗 (`UpdateNoticeForm`) 与进度动效 100% 继承 `ModernThemeHelper` 视觉规范，无多余白边或 Windows 原生丑陋对话框。
5. **零干扰静默体验 (Zero-Interruption Experience)**：开机自启静默检测延迟 10 秒发起，网络失败时不弹出任何阻断性错误；在游戏中静默检测发现新版时采用轻量托盘提示，绝不抢占前台焦点。

---

## 2. 系统架构与拓扑结构 (System Architecture)

```
[云端 GitHub 公开分发仓库: Ferris-echo/FerrisPulse]
   │
   ├── README.md (软件简介与特性说明)
   ├── version.json (版本号、更新日志、直链、SHA256)
   └── Releases (v4.3.0/FerrisPulse.exe 混淆构建包)
   │
   ▼ (国内双通道加速镜像: ghproxy.net / mirror.ghproxy.com)
   │
[本地客户端: FerrisPulse.exe]
   │
   ├── [UpdateManager (核心更新服务)]
   │     ├── CheckUpdateAsync() ──> 异步探测 version.json 并比较 SemVer
   │     ├── DownloadUpdateAsync() ──> 流式下载到 FerrisPulse.exe.update 并校验 SHA256
   │     └── LaunchSelfReplace() ──> 唤醒原子置换伴随进程并退出主程序
   │
   ├── [AboutForm (专属黑金关于与致谢卡片)]
   │     ├── 顶部: 绿色发光 Logo + FerrisPulse 品牌名 + 版本徽章
   │     ├── 中部: 作者署名 Ferris-echo + [立即检查更新] 按钮 + 自动更新开关
   │     └── 底部: 微信/支付宝赞赏二维码卡片 (智能加载 donate.png)
   │
   ├── [UpdateNoticeForm (暗黑高质感更新界面)]
   │     ├── 状态A: 展示新版本徽章、发布日期、暗黑日志滚动卡片
   │     ├── 状态B: 就地展开 ModernProgressBar 流光进度条与实时下载速率
   │     └── 状态C: 提示就绪，触发原子热替换
   │
   ├── [设置与托盘交互入口]
   │     ├── MainForm 顶栏右上角: [ ℹ️ ] 关于与致谢图标按钮 ──> 打开 AboutForm
   │     ├── TrayMenu 托盘项: [关于 FerrisPulse] 与 [检查更新]
   │     └── 周期性后台探测 (每 3 天探测一次 + 开机延迟 10 秒静默探测)
   │
   └── [编译与发布工具链: build.bat]
         ├── CSC 4.0 编译源码 ──> Raw FerrisPulse.exe
         ├── Obfuscar 混淆引擎 ──> 符号重命名 + 字符串加密 + 控制流混淆 ──> Obfuscated FerrisPulse.exe
         └── 打包发布 ──> FerrisPulse_vX.Y_Portable.zip
```

---

## 3. 云端协议契约 (`version.json`)

在 GitHub 仓库根目录维护 `version.json`（UTF-8 with BOM 或标准 UTF-8）：

```json
{
  "latestVersion": "4.3.0",
  "releaseDate": "2026-10-05",
  "minSupportedVersion": "4.0.0",
  "downloadUrl": "https://github.com/Ferris-echo/FerrisPulse/releases/download/v4.3.0/FerrisPulse.exe",
  "sha256": "4a7d6e8b9c0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b",
  "fileSize": 12845056,
  "changelog": [
    "【新增】全自动在线检查更新与原地热替换功能，告别手动覆盖",
    "【新增】支持开机自启静默更新与周期性检测，设置页增设自动更新开关",
    "【优化】全套界面统一暗黑美学升级，更新界面动效丝滑流畅",
    "【优化】外设连接探测与后台待机休眠稳定性全面增强"
  ]
}
```

### 3.1 客户端数据模型 (`src/Models/UpdateInfo.cs`)
```csharp
public class UpdateInfo
{
    public string LatestVersion { get; set; }
    public string ReleaseDate { get; set; }
    public string MinSupportedVersion { get; set; }
    public string DownloadUrl { get; set; }
    public string Sha256 { get; set; }
    public long FileSize { get; set; }
    public List<string> Changelog { get; set; }

    public UpdateInfo()
    {
        Changelog = new List<string>();
    }
}
```

### 3.2 节点轮询策略
客户端请求 URL 构造优先级：
1. `https://ghproxy.net/https://raw.githubusercontent.com/Ferris-echo/FerrisPulse/main/version.json`
2. `https://mirror.ghproxy.com/https://raw.githubusercontent.com/Ferris-echo/FerrisPulse/main/version.json`
3. `https://raw.githubusercontent.com/Ferris-echo/FerrisPulse/main/version.json`（直连备用）

每次请求超时设置为 3000ms（探测包仅数百字节），失败自动顺延下一节点。

---

## 4. 界面设计规范与交互逻辑 (`AboutForm.cs` & `UpdateNoticeForm.cs`)

### 4.1 专属黑金关于与致谢卡片 (`AboutForm.cs`)
- **视觉风格**: 纯粹黑金电竞质感，尺寸 460 × 520 px，无边框圆角（8px），DWM 11+ 微光阴影。
- **布局结构**:
  1. **标题栏**: 左侧绿色闪电微标 + `关于 FerrisPulse`，右侧紧凑 `✕` 关闭按钮，支持任意空白区拖拽。
  2. **品牌与作者致谢区**:
     - 居中渲染高质感绿色呼吸发光软件 Logo（`app.ico` 矢量重绘）。
     - 软件名称: `FerrisPulse · 灵脉` (16pt 粗体，白色高亮)。
     - 版本徽章: `v4.2.0 旗舰版` (绿色发光胶囊微章 `StatusPill`)。
     - 开发者署名: `由 Ferris-echo 独立设计与开发` (柔和浅灰)。
     - 标语: `专为电竞玩家打造的极致低功耗多品牌外设管家` (9pt 暗灰)。
  3. **在线更新与偏好卡片**:
     - 暗黑卡片背景 (`#1E1E1E`)，边框 (`#2A2A2A`)。
     - 当前版本状态与 `[ 立即检查更新 ]` 按钮（暗黑边框，点击发起异步探测）。
     - `☑ 开机与后台自动检查更新` 开关（复选框），同步持久化至注册表 `AutoCheckUpdate`。
  4. **赞赏支持卡片 (Support & Donate)**:
     - 标题: `☕ 支持作者 / 赞赏开发`。
     - 寄语: `如果您喜欢这款软件，欢迎请作者喝杯咖啡，支持持续迭代！`。
     - 居中展示圆角暗黑赞赏卡片（180 × 180 px）：
       - 智能自适应加载同级目录下 `donate.png` 或 `qr.png`（高抗锯齿居中平铺）。
       - 若未放置，则渲染精致的矢量暗黑占位卡片与说明：“微信 / 支付宝 赞赏码”。
     - 底部链接: `GitHub: Ferris-echo/FerrisPulse`。

### 4.2 升级通知与流光下载弹窗 (`UpdateNoticeForm.cs`)
- **尺寸**: 460 × 420 px，无边框暗黑圆角，DWM 11+ 微光阴影。
- **布局与状态流转**:
  1. **标题栏**: `⚡ 发现新版本` + 右侧关闭按钮。
  2. **版本头部**: 绿色发光微章（如 `v4.3.0 旗舰版`）+ 发布日期。
  3. **日志容器**: 凹面暗黑卡片（#1E1E1E），支持滚轮滑动，展示条目化更新日志。
  4. **操作与进度流转**:
     - **待更新**: 左侧 `[ 稍后提醒 ]`（暗灰幽灵按钮），右侧 `[ 立即升级 ]`（雷蛇绿发光主按钮）。
     - **下载中**: 点击升级后按钮隐藏，原地展开 `ModernProgressBar`（绿色流光渐变），下方显示动态速率与百分比。
     - **就绪安装**: 满格提示“下载完成，正在自动重启应用更新...”，1.2 秒后唤醒原子置换命令并退出。

### 4.3 主界面与托盘入口
- **主界面顶栏 (Top Bar)**:
  - 保持主界面紧凑原生（不塞多余大设置卡片），在顶栏右上角 `[ 📶 ]` 与 `[ ➕ ]` 旁边新增 **`[ ℹ️ ]`**（关于与致谢图标按钮）。
  - 悬浮气泡提示: `关于 FerrisPulse & 赞赏支持`，点击优雅弹出 `AboutForm`。
- **系统托盘右键菜单**:
  - 纯净化菜单项（零图标、零 Emoji）：
    - `关于 FerrisPulse`: 点击弹出 `AboutForm`。
    - `检查更新`: 点击直接发起检测（若有新版弹出 `UpdateNoticeForm`，若最新弹出温和气泡通知）。

---

## 5. 无脑全自动原地热替换引擎 (In-Place Self-Updating)

### 5.1 Windows 文件锁机制与突破原理
在 Windows 环境下，操作系统在可执行文件运行时对其施加 `FILE_SHARE_READ` 独占写入锁，任何外部进程或本进程都无法直接对运行中的 `FerrisPulse.exe` 进行 `File.Write` 或覆盖。  
然而，Windows 允许在原进程退出（Release Handle）后的瞬间执行文件重命名或移动覆盖。

### 5.2 原子置换流程
1. **下载与校验**:
   - `UpdateManager` 将远端二进制流写入当前运行目录下的临时文件 `FerrisPulse.exe.update`。
   - 计算并对比远端提供的 `sha256`。若哈希不匹配或文件大小异常，删除临时文件，抛出明确提示，绝不进入替换流程。
2. **生成自愈伴随更新命令**:
   - 获取当前进程 PID：`int currentPid = Process.GetCurrentProcess().Id;`
   - 获取当前可执行文件的绝对路径：`string exePath = Application.ExecutablePath;`
   - 构造极其轻量、隐藏无黑框的替换命令（通过 `ProcessStartInfo` 设置 `WindowStyle = ProcessWindowStyle.Hidden; CreateNoWindow = true;`）：
   ```csharp
   string cmd = string.Format(
       "powershell -NoProfile -WindowStyle Hidden -Command \"" +
       "try {{ " +
       "  Wait-Process -Id {0} -Timeout 10; " +
       "}} catch {{}} " +
       "Start-Sleep -Milliseconds 300; " +
       "Move-Item -LiteralPath '{1}.update' -Destination '{1}' -Force; " +
       "Start-Process -FilePath '{1}';\"",
       currentPid, exePath.Replace("'", "''")
   );
   ```
3. **释放句柄与进程唤醒**:
   - 伴随后台进程拉起后，主程序立即调用 `Application.Exit()` 或 `Environment.Exit(0)`。
   - 旧进程彻底关闭 -> PowerShell 伴随进程检测到旧 PID 退出 -> 瞬间将 `FerrisPulse.exe.update` 强行覆盖至 `FerrisPulse.exe` -> `Start-Process` 拉起新版本 -> 伴随进程自毁退出。
   - **终极体验**：整个更新过程用户无需打开文件管理器，无需解压，主窗口闪退 1 秒后自动重新亮起新版，配置毫发无损。

---

## 6. 闭源防反编译混淆流水线 (Anti-Decompilation Pipeline)

### 6.1 逆向风险与防护等级
.NET 程序编译后的中间语言（CIL/MSIL）保留了所有类型名称、方法签名、局部变量与控制流，反编译器（ILSpy/dnSpy）可在 0.1 秒内逆向还原出几乎完全等同的 C# 源码。  
为了彻底闭源，我们实施三大防护：
1. **类型与成员重命名 (Renaming)**：所有非导出类型、类名、方法名、私有字段重命名为 `a`, `b`, `c` 或不可见控制字符。
2. **字符串动态加密 (String Encryption)**：对所有硬编码的 HID 报文字节头、URL、注册表键名、设备名进行 XOR 或 AES 动态加载加密，防止反汇编静态搜索。
3. **控制流混淆 (Control Flow Flattening)**：破坏结构化分支，插入混淆跳转指令，使反编译器在尝试反编译反汇编代码时发生解析错误或呈现复杂迷宫代码。

### 6.2 混淆工具链集成 (`build.bat`)
在根目录 `tools/obfuscator/` 部署轻量单文件混淆器（如 **Obfuscar** 2.2.x 针对 .NET 4.0 配置好的控制台工具），并编写规则配置文件 `tools/obfuscator/obfuscar.xml`：

```xml
<?xml version='1.0'?>
<Obfuscator>
  <Var name="InPath" value="." />
  <Var name="OutPath" value=".\Obfuscated" />
  <Var name="KeepPublicApi" value="false" />
  <Var name="HidePrivateApi" value="true" />
  <Var name="RenameProperties" value="true" />
  <Var name="RenameEvents" value="true" />
  <Var name="RenameFields" value="true" />
  <Var name="RenameMethods" value="true" />
  <Var name="RenameParameters" value="true" />
  <Var name="ReuseNames" value="true" />
  <Var name="HideStrings" value="true" />
  <Var name="OptimizeMethods" value="true" />
  
  <Module assembly="$(InPath)\FerrisPulse.raw.exe">
    <!-- 保留 Windows Forms 和 Win32 消息泵必要入口 -->
    <SkipType name="FerrisPulse.Program" />
  </Module>
</Obfuscator>
```

构建流水线优化为：
1. `csc.exe` 编译生成原始未混淆二进制文件 `FerrisPulse.raw.exe`。
2. 调用混淆引擎生成加固文件 `Obfuscated\FerrisPulse.exe`。
3. 自动化测试套件执行混淆后产物的核心功能回归测试。
4. 校验通过后，将混淆后的 `FerrisPulse.exe` 输出为根目录正式发布文件并打包为 Release zip。

---

## 7. 业务逻辑与设置项集成 (Integration & Triggering)

### 7.1 配置项规范 (`src/Core/ConfigManager.cs`)
在注册表路径 `HKCU\Software\FerrisPulse` 中新增配置项：
- `AutoCheckUpdate` (DWORD, 默认 `1`): 是否启用后台自动静默检查更新。
- `LastUpdateCheckTime` (String): 上次静默检测的 ISO 时间戳，防止重复高频探测。
- `IgnoredUpdateVersion` (String): 用户选择“稍后提醒”或跳过的特定版本（可选）。

### 7.2 触发时机与控制流
1. **手动点击“检查更新”**（关于页 / 托盘菜单）：
   - 立即异步发起探测，UI 显示“正在检查更新...”。
   - 若检测到新版本：立即弹出 `UpdateNoticeForm`。
   - 若当前已是最新版：弹出轻量暗黑提示卡片或 Toast：“当前已是最新版本 (v4.2.0)”。
   - 若网络超时或失败：弹出温和提示：“无法连接到更新服务器，请检查网络后重试”。
2. **开机自启动与日常周期静默探测**：
   - 开机自启延迟 **10 秒** 后在后台静默发起探测（避开系统开机网络初始化高潮）。
   - 日常运行每 72 小时（3 天）静默探测一次。
   - 若网络不可达或已是最新版：**完全静默，绝不打扰用户**。
   - 若探测到新版本：**直接弹出高颜值 `UpdateNoticeForm` 升级对话框**（与手动检查效果一致，无需让用户在托盘气泡中多点一次），清晰呈现版本特性，用户可直接点击“立即升级”或“稍后提醒”。

---

## 8. 测试与质量验证体系 (Verification Matrix)

在实施阶段必须严格遵循 TDD 规范，新增并执行以下自动化测试：

1. **版本比较引擎单元测试 (`tests/test_version_comparison.ps1`)**:
   - 验证 SemVer 解析：`4.2.0` < `4.3.0`，`4.2.0` == `4.2.0`，`4.2.1` > `4.2.0`，支持两段式与三段式版本号。
2. **云端探测协议与镜像容灾测试 (`tests/test_update_metadata_contract.ps1`)**:
   - 验证 `version.json` 模式有效性与哈希字段完整性。
   - 模拟代理节点故障与多镜像自动故障转移逻辑。
3. **配置项读写与默认值测试 (`tests/test_update_config_policy.ps1`)**:
   - 验证 `AutoCheckUpdate` 在注册表中的默认创建与读写持久化。
4. **暗黑界面渲染与控件适配测试 (`tests/test_update_ui_presentation.ps1`)**:
   - 验证 `UpdateNoticeForm` 颜色无白边、双缓冲无闪烁、DPI 缩放合规。
5. **全自动原地热替换原子测试 (`tests/test_in_place_self_update.ps1`)**:
   - 模拟临时文件写入与伴随脚本启动，确保旧进程退出后新文件无缝覆盖并成功唤醒。
6. **混淆产物有效性与反编译防御校验 (`tests/test_obfuscation_integrity.ps1`)**:
   - 验证混淆后生成的 `FerrisPulse.exe` 依然保持正常运行、托盘加载无异常，且关键私有方法名与字段已被符号粉碎。
7. **全量回归测试**:
   - 运行 `tools/run_all_tests.ps1`，确保全部现有 15 项测试与新增更新测试 100% 全绿。

---

## 9. 演进路线与分步实施计划 (Implementation Breakdown)

- **Phase 1: 协议、模型与更新管理器服务 (`src/Models/`, `src/Services/UpdateManager.cs`)**
- **Phase 2: 统一暗黑视觉弹窗与动效实现 (`src/UI/Forms/UpdateNoticeForm.cs`)**
- **Phase 3: 设置界面、托盘菜单与生命周期联动 (`MainForm.UI.cs`, `MainForm.Tray.cs`, `Program.cs`)**
- **Phase 4: 原子原地置换引擎与安全自重启落地**
- **Phase 5: 编译流水线集成代码混淆保护与打包规范 (`build.bat`)**
- **Phase 6: 自动化测试套件编排、全量回归验证与 Release 发布**
