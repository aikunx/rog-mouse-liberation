# 02 · 已安装程序与运行时数据面（ROG STRIX IMPACT III WIRELESS / P520）

> 采集时间：2026-10-18 02:36–03:10（Asia/Shanghai）
> 采集方式：只读。Node 直读文件系统、PowerShell 读注册表/服务/PE 版本、netstat 观察端口。
> 边界：未修改、未删除、未移动 `C:\Program Files (x86)\ASUS` 与 `C:\ProgramData\ASUS` 下任何文件；未启动任何产物 exe。
> 体积口径：同时给字节数与 MiB（1 MiB = 1,048,576 B）；资源管理器「大小」为十进制 MB，两者差约 4.8%。

---

## 0. 结论速览

| 结论 | 依据 |
|---|---|
| 四顶层目录实测 1,754 文件 / 387.28 MiB，其中 `ArmouryDevice` 占 1,708 文件 / 346.02 MiB | 全树递归统计（第 1 节） |
| `AcLoader` 在安装目录只剩 1 个日志；真正的 AC Loader 载荷在 `ArmouryDriverUpdate\AP_UPDATE\AC_loader_v33060` | `AcLoader\AcLoader.log`；注册表 `HKLM\SOFTWARE\ASUS\ROGLiveServicePackage` |
| 主程序 `asus_framework.exe` = 4.2.2.8「ASUS NodeJS Web Framework」，Electron 29.4.6，清单要求 requireAdministrator | PE 版本资源；`asus_framework.exe.manifest`；app.asar `package.json` |
| 端口 1042/1043 是硬编码默认值，运行期把实际端口写进注册表 | asar 模块 3147 `ports:{http:"1042",https:"1043"}`；注册表 `HTTPPort/HTTPSPort` |
| `ArmourySocketServer.exe` 是「拉起 + 看护」框架的一方，不是框架的子进程 | 字符串 `Run asus_framework` / `Stop watchdog for asus_framework` / `CreateProcessW` / `Global\ArmourySocketServerMutex`；GearInstall 日志 `execute ArmourySocketServer.exe Done.` |
| `modules\mouse` / `unifyDongle` 由框架主进程 checkModules() 用 require() 挂成 Express 路由（deviceType 1/21），不是 ac_node_addon 桥 | asar `main/index.js`；两个 index.js 导出 `{router, deviceType}` |
| 硬件链路：命令 XML → 框架 HTTP 路由 → utilityProcess.fork(sdk.js) → ArmouryMsSDK.dll!ExecuteFunction | 日志 `{in} ExecuteFunction` 与 strInputXml；asar `forkProcess` |
| 设备页实际 4 个（6862/6871/omni/p520），`View\6873` 不存在；6873 只是 P520 的蓝牙 PID | `dll\MouseSDK\config.ini [44] BL_PID=6873`；注册表 `ArmouryHttp\6873`（页面未落盘） |
| `server.pem` 是 WebSocket++ 官方示例证书（Zaphoyd / Peter Thorson），自签且 2012-11-15 已过期 | 证书 Subject=Issuer、SHA1-RSA、2048 位；同目录 exe 含 `WebSocket++/0.8.2` |
| 已装固件与安装包逐字节一致（15/15 文件 SHA-256 相同） | 第 6 节对比表 |
| 计划任务无法查询（权限拒绝），任务名 `\ASUS\Framework Service` 仅由二进制字符串推断 | `AsTaskSchedArg.exe` 含 `/change /tn "ASUS\Framework Service" /DISABLE`；schtasks/Get-ScheduledTask 均被拒（第 9 节） |

---

## 1. 四个顶层目录：定位与体积

`C:\Program Files (x86)\ASUS` 下只有 4 个目录，无散落文件。

| 目录 | 文件数 | 字节 | MiB | 十进制 MB | 定位（依据） |
|---|---:|---:|---:|---:|---|
| `AcLoader` | 1 | 14,393 | 0.01 | 0.01 | 只剩 `AcLoader.log`；用途是 AC Loader 的下载引导（日志显示它把 IDE 与 `AC_loader_v33060.zip` 下载进 `ArmouryDriverUpdate`） |
| `ArmouryDevice` | 1,708 | 362,826,174 | 346.02 | 362.83 | 框架 + 设备页 + SDK + 固件主安装体（第 2–7 节） |
| `ArmouryDriverUpdate` | 36 | 20,136,340 | 19.20 | 20.14 | Armoury Crate 安装器分发区（`AP_UPDATE\AC_loader_v33060(.zip)` + IDE 索引）；`ROGLiveServicePackage.ACInstallPath` 指向此处 |
| `AsusROGLSLService` | 9 | 3,694,928 | 3.52 | 3.69 | ROG Live Service：AC 安装/引导 UI、UWP 检测、CheckTool |

合计 **1,754 文件 / 387.28 MiB**。

> 与背景「ArmouryDevice 373MB」差异：实测 346.02 MiB = 362.83 MB（十进制）。即便按 1,708 个文件 × 4 KiB 簇占用估算也只多约 6.7 MB。**推断**该数值来自另一时刻/另一台机器快照，或把 `C:\Program Files\ASUS\Aac_Mouse`（HAL，约 5.9 MiB）一并计入（见第 3.6 节）。

### 1.1 逐目录说明

**AcLoader**（`C:\Program Files (x86)\ASUS\AcLoader\AcLoader.log`，14,393 B）

```text
2026-09-30 23:21:27, : Driver Need Download and Install OOOOOO
2026-09-30 23:21:30, : Driver---IDE Download from: https://liveupdate01s.asus.com/pub/ASUS/LiveUpdate/Release//Motherboard//ARMOURY CRATE.ide
2026-09-30 23:21:30, : Driver---IDE Download to: C:\Program Files (x86)\ASUS\ArmouryDriverUpdate\ARMOURY CRATE\ARMOURY CRATE.ide
2026-09-30 23:21:30, : Driver---Download from: https://liveupdate01s.asus.com/pub/ASUS/mb/14Utilities/AC_loader_v33060.zip
2026-09-30 23:21:30, : Driver---Download to: C:\Program Files (x86)\ASUS\ArmouryDriverUpdate\AP_UPDATE\AC_loader_v33060.zip
2026-09-30 23:21:32, : Driver Downloaded and unzip ok!!!
```

旁证：`C:\ProgramData\ASUS\ALL\ACLoader\` 下有 `ACSE.ini`（`[Main] IsROGConsoleProduct=0`）与 `ROGLSLoaderUI_*.log`（启动参数 `-roglsloaderstart -win armourycrate -acinstaller`）。即 AcLoader 的执行体由 Live Service 下载后运行，不常驻安装目录。

**ArmouryDriverUpdate**（完整 36 文件清单）

| 路径 | 大小 | 说明 |
|---|---:|---|
| `AP_UPDATE\AC_loader_v33060.zip` | 7,266.8 KB | AC Loader 3.3.0.60 压缩包（与 IDE 下发同一份） |
| `AP_UPDATE\AC_loader_v33060\ArmouryCrateInstaller.exe` | 235.4 KB | 1.0.8.0「Driver Model Setup Program」 |
| `AP_UPDATE\AC_loader_v33060\ArmouryCrateInstaller.exe.config` | 0.8 KB | .NET 配置 |
| `AP_UPDATE\AC_loader_v33060\UWPInstaller.exe` | 4,208.9 KB | 3.3.6.0「The installer for Armoury Crate and Aura Creator」 |
| `AP_UPDATE\AC_loader_v33060\UnifyScanner.dll` | 343.9 KB | 1.0.0.20 |
| `AP_UPDATE\AC_loader_v33060\BouncyCastle.Cryptography.dll` | 6,706.3 KB | 2.4.0.33771 |
| `AP_UPDATE\AC_loader_v33060\NuGet.Core.dll` | 650.7 KB | 2.14.0.832 |
| `AP_UPDATE\AC_loader_v33060\Microsoft.Web.XmlTransform.dll` | 79.7 KB | 2.1.50430.0 |
| `AP_UPDATE\AC_loader_v33060\System.Numerics.Vectors.dll` | 113.1 KB | 4.6.26515.06 |
| `AP_UPDATE\AC_loader_v33060\private_key.pem` | 9.4 KB | RSA 私钥（敏感；仅记录存在，未读内容） |
| `AP_UPDATE\AC_loader_v33060\language\*.xml` ×22 | 0.9–1.9 KB | 捷克/丹麦/荷兰/英/芬/法/德/印尼/意/日/韩/挪/波/葡/罗/俄/简中/斯洛伐克/斯洛伐克(重复命名)/西/瑞典/泰/繁中/土/乌 |
| `ARMOURY CRATE\ARMOURY CRATE.ide` | 21.2 KB | Live Update 的 IDE 索引 |

**AsusROGLSLService**（完整 9 文件清单）

| 文件 | 大小 | 版本 / 公司 / 描述 |
|---|---:|---|
| `Armoury Crate Installer.exe` | 1,222.9 KB | 3.3.5.2 / ASUSTeK COMPUTER INC. / Armoury Crate Installer |
| `AsusROGLSLService.exe` | 666.9 KB | 3.3.2.1 / ASUSTeK COMPUTER INC. |
| `AsusGCGridServiceSetup.exe` | 270.3 KB | 1.1.1.0 / 无公司 |
| `AsZip.dll` | 162.9 KB | 1,0,0,1 / AsZip DLL |
| `AsZip.exe` | 20.5 KB | 1.0.0.2 |
| `CheckTool.exe` | 26.9 KB | 1.0.2.0 / CheckTool |
| `Newtonsoft.Json.dll` | 685.5 KB | 13.0.1.25517 / Newtonsoft |
| `ProductInfoLib.dll` | 233.6 KB | 1.0.0.4 / ASUSTeK COMPUTER INC. / ProductInfoLib |
| `QueryUWP.exe` | 318.8 KB | 1.0.0.3 |

`AsusROGLSLService.exe` 字符串：`RegisterServiceCtrlHandlerEx`、`createService`、`ROG Live Service is (not) installed`、`Launch QueryUWP`、`runCmd(CheckTool.exe)`、`ROGLSLoaderUI`、`[ArmouryCrateInstallDialog] Put Install Button`、`Write InstallFinish`。→ 它是「检测 AC → 弹安装对话框 → 拉起 ROGLSLoader」的服务/引导器，与 `HKLM\SOFTWARE\ASUS\ROGLiveServicePackage`（`ACInstallPath=...\AC_loader_v33060`、`InstallFinish=1`）闭环。

---

## 2. ArmouryDevice 顶层文件逐个说明

| 文件 | 大小 | FileVersion / ProductVersion | 公司 | 描述 | 说明 |
|---|---:|---|---|---|---|
| `asus_framework.exe` | 148,515.3 KB（≈152.08 MB） | 4.2.2.8 / 4.2.2.8 | ASUSTeK COMPUTER INC. | ASUS NodeJS Web Framework | Electron 主程序；`requestSingleInstanceLock`；清单 requireAdministrator |
| `asus_framework.exe.manifest` | 1.5 KB | — | — | — | `assemblyIdentity version="8.9.0.0"`；`requireAdministrator`；声明 Win7–Win10 兼容 |
| `asus_framework.VisualElementsManifest.xml` | 0.2 KB | — | — | — | `BackgroundColor=black`，UWP 磁贴视觉元素 |
| `chrome_100_percent.pak` | 150.8 KB | — | — | — | Chromium UI 资源 |
| `chrome_200_percent.pak` | 229.6 KB | — | — | — | 同上（高分屏） |
| `resources.pak` | 5,157.5 KB | — | — | — | Chromium 资源包 |
| `icudtl.dat` | 10,466.5 KB | — | — | — | ICU 数据（i18n） |
| `snapshot_blob.bin` | 305.7 KB | — | — | — | V8 启动快照 |
| `v8_context_snapshot.bin` | 665.0 KB | — | — | — | V8 上下文快照 |
| `version` | 6 B | — | — | — | 内容 `29.4.6`（Electron 版本，非产品版本） |
| `ffmpeg.dll` | 2,489.9 KB | 无版本资源 | — | — | Electron 媒体解码（32 位） |
| `libEGL.dll` | 376.4 KB | 2.1.22487 git hash: 987ecc0d99f6 | — | ANGLE libEGL Dynamic Link Library | ANGLE |
| `libGLESv2.dll` | 6,447.9 KB | 2.1.22487 git hash: 987ecc0d99f6 | — | ANGLE libGLESv2 Dynamic Link Library | ANGLE |
| `d3dcompiler_47.dll` | 4,030.5 KB | 10.0.22621.755 | Microsoft Corporation | Direct3D HLSL Compiler for Redistribution | 系统组件重分发 |
| `vulkan-1.dll` | 823.4 KB | Vulkan Loader - Dev Build | — | 1.3.275.Dev Build | Vulkan loader |
| `vk_swiftshader.dll` | 4,502.4 KB | 5.0.0 | — | SwiftShader Vulkan 32-bit Dynamic Link Library | 软件 Vulkan |
| `vk_swiftshader_icd.json` | 0.1 KB | — | — | — | `{"file_format_version":"1.0.0","ICD":{"library_path":".\\vk_swiftshader.dll","api_version":"1.0.5"}}` |
| `cpprest141_2_10.dll` | 2,556.3 KB | 无版本资源 | — | — | C++ REST SDK 2.10（固件工具与 SDK 共用） |
| `MsFWUpdate.dll` | 435.3 KB | 1.0.0.37 | TODO: <Company name> | TODO: <File description> | 固件更新封装（安装包 `Firmware\Tools\Tools\` 同源文件装在根目录） |
| `MD5.ini` | 7,467 B | — | — | — | 安装器校验清单：`[FilesToCheck] Count=48`，路径为 `D:\Jenkins\workspace\FrameworkServiceSetup_InstallShield\...`；尾部 `[MD5] Value=1709681547 / InstalledValue=1709681547 / Matched=0`（**是 32 位整数校验值，不是十六进制 MD5**，推断为安装器自定义累加校验） |
| `LICENSE` | 1.1 KB | — | — | — | Electron 官方 MIT（Electron contributors / GitHub Inc.） |
| `LICENSES.chromium.html` | 9,119.9 KB | — | — | — | Chromium 第三方许可证汇总 |
| `dll\` | 目录 | — | — | — | 见第 3 节 |
| `Firmware\` | 目录 | — | — | — | 见第 6 节 |
| `locales\` | 目录 | — | — | — | 仅 `en-US.pak`，见第 7 节 |
| `modules\` | 目录 | — | — | — | 见第 4 节 |
| `resources\` | 目录 | — | — | — | 见第 5 节 |
| `View\` | 目录 | — | — | — | 见第 7 节 |

> 无老式 `swiftshader\` 子目录：本包 SwiftShader 只走 `vk_swiftshader.dll` 单文件。

---

## 3. `dll\` 目录

```text
dll\
├─ ArmourySDK.dll              324.8 KB  2.0.24.0  ASUSTeK Computer Inc.  ArmourySDK.dll
├─ InstallHelper.dll           268.8 KB  1.0.0.35  ASUSTeK COMPUTER INC.  ROG Armoury InstallHelper
├─ config.ini                  749 B     SDK 类型→DLL 路径映射（13 条）
├─ Process.ini                 432 B     进程 / 自启动 / 卸载助手清单
├─ MD5.ini                     58.0 KB   安装校验清单
├─ ArmourySocketServer\        3.2
├─ MouseSDK\                   3.3
├─ Schedule\                   3.4
├─ Tools\                      3.4
└─ UnifyDongleSDK\             3.5
```

### 3.1 根级文件与两个 ini 全文

**`dll\config.ini`（全文 749 B）**——13 个 SDK 插件路径映射，框架启动时据此逐个加载（日志 `SDK list count 13` 与 `Count =13` 吻合）：

```ini
[ArmourySDK]
Count =13
[1]
Type ID =1
Path =\MouseSDK\ArmouryMsSDK.dll
[2]
Type ID =21
Path =\UnifyDongleSDK\AC_UnifyDongleSDK.dll
[3]
Type ID =2
Path =\KeyboardSDK\ArmouryKbSDK.dll
[4]
Type ID =4
Path =\AudioSDK\ArmouryAudioSDK.dll
[5]
Type ID =7
Path =\MousePadSDK\ArmouryMsPadSDK.dll
[6]
Type ID =5
Path =\AIOFanSDK\ArmouryAIOSDK.dll
[7]
Type ID =6
Path =\OledSDK\ArmouryOledSDK.dll
[8]
Type ID =8
Path =\MBLedSDK\ArmouryMBLedSDK.dll
[9]
Type ID =10
Path =\NodeSDK\ArmouryNodeSDK.dll
[10]
Type ID =9
Path =\SensorBoxSDK\ArmourySensorBoxSDK.dll
[11]
Type ID =13
Path =\VgaSDK\ArmouryVgaSDK.dll
[12]
Type ID =14
Path =\DisplaySDK\ArmouryDisplaySDK.dll
[13]
Type ID=15
Path=\FanCardSDK\ArmouryFanCardSDK.dll
```

> 13 条里只有 `MouseSDK`、`UnifyDongleSDK` 两个目录真实存在（其余 11 个 SDK 未随本鼠标包安装）。这与 `Framework.00.*.log` 的 `6862 is not defined in the config.ini of SDK` 属同一机制：Type ID 与已装 DLL 对不上即报错。

**`dll\Process.ini`（全文 432 B）**：

```ini
[Process]
Count = 7
0 = ArmourySwAgent.exe
1 = FanOSD.exe
2 = ArmourySocketServer.exe
3 = asus_framework.exe
4 = P508PowerAgent.exe
5 = P508PowerAgent.exe
6 = AcPowerNotification.exe

[Startup]
Count = 2
0 = \SwAgent\ArmourySwAgent.exe
1 = \AcPowerNotification\AcPowerNotification.exe

[StartupCmd]
Count = 2
0 = -s
1 = 

[UninstallHelper]
Count = 1
0 = \ArmourySocketServer\PreUninstallArmourySocketServer.exe
```

- 用途：升级/卸载前按 `[Process]` 杀进程；`[Startup]` 声明框架要拉起的伴随进程（`ArmourySwAgent.exe -s`、`AcPowerNotification.exe`）；`[UninstallHelper]` 指向 SocketServer 预卸载程序。
- `P508PowerAgent.exe` 连续出现两次是原文件即如此（未改动）。

**`dll\MD5.ini`（58.0 KB）**：结构同顶层 `MD5.ini`（`[Device]/[FilesToCheck]/[MD5]`），路径全为 `D:\Jenkins\workspace\...` 构建产物路径；`Matched=0` 为常态（该字段安装时改写）。

**`ArmourySDK.dll`** 2.0.24.0：除 `dll\config.ini` 外，框架代码另有兜底分支——设备类型非 21 时 `a="ArmourySDK", i={deviceType:"ArmourySDK", path:"/ArmourySDK.dll"}`，即 MainSDK 自身也走同一加载器。

**`InstallHelper.dll`** 1.0.0.35「ROG Armoury InstallHelper」：供安装/卸载与签名校验（`checkForeignLib`、`isWinVerifyTrust`）使用。

### 3.2 `dll\ArmourySocketServer\`

| 文件 | 大小 | 版本 / 公司 | 说明 |
|---|---:|---|---|
| `ArmourySocketServer.exe` | 1,739.3 KB | 0.1.20.21 / ASUSTeK COMPUTER INC. | 单实例守护进程（`Global\ArmourySocketServerMutex`），拉起并看护 asus_framework |
| `ArmourySocketServer.VisualElementsManifest.xml` | 0.2 KB | — | UWP 磁贴视觉元素 |
| `AuraIC_x64.dll` | 2,811.3 KB | 1.0.3.0 / ASUSTeK Computer Inc. | AURA IC control interfaces |
| `libcrypto-1_1-x64.dll` | 3,351.3 KB | 1.1.1-dev / The OpenSSL Project | OpenSSL 1.1.1 |
| `libssl-1_1-x64.dll` | 679.8 KB | 1.1.1-dev / The OpenSSL Project | OpenSSL 1.1.1 |
| `PreUninstallArmourySocketServer.exe` | 212.8 KB | 1.0.0.4 / ASUS | 预卸载助手（`Process.ini[UninstallHelper]`） |
| `server.pem` | 3.5 KB | — | TLS 私钥 + 证书（见下） |
| `licenses.txt` | 13.5 KB | — | 第三方许可证（WebSocket++/OpenSSL 等） |
| `device_language\*.xml` ×24 | 152–299 KB | — | 24 语种设备/按键功能名表（`Thai.xml` 299,032 B 最大，`Traditional Chinese.xml` 152,762 B 最小） |
| `Resource\` | 3 PNG | — | `5stage_1.png`(531 B)、`rapid-fire-off@2x.png`(645 B)、`rapid-fire-on@2x.png`(716 B)，按键面板「5 段 / 连发」状态图标 |

**`server.pem`：确认为自签，且是 WebSocket++ 的示例证书。**

| 字段 | 值 |
|---|---|
| Subject | `E=webmaster@zaphoyd.com, CN=Peter Thorson, OU=WebSocket++, O=Zaphoyd Studios, L=Chicago, S=Illinois, C=US` |
| Issuer | 与 Subject 完全相同 → **自签（self-signed）** |
| 有效期 | 2011-11-16 05:20:06 → **2012-11-15 05:20:06（已过期）** |
| 序列号 | `00CE4CB8A25ECD7AB4` |
| 签名算法 / 密钥 | `sha1RSA` / RSA 2048 |
| 私钥 | `-----BEGIN RSA PRIVATE KEY-----`，`Proc-Type: 4,ENCRYPTED`，`DEK-Info: DES-EDE3-CBC, A0ED66EF872A48A9` |

> 结论：这不是 ASUS 自建 CA 签发，而是 websocketpp 仓库自带的示例证书（同目录 exe 内也有 `WebSocket++/0.8.2`）。它只用于本机 wss 自环，不能当作产品 TLS 设计解读。

### 3.3 `dll\MouseSDK\`

| 文件 | 大小 | 版本 / 公司 | 说明 |
|---|---:|---|---|
| `ArmouryMsSDK.dll` | 1,186.8 KB | 3.1.48.0（日志自报 `3.01.48`）/ TODO: <Company name> | 鼠标 SDK 主体，导出 `ExecuteFunction(LPCWSTR xml, IntPtr* pOut)` |
| `dexin.exe` | 571.9 KB | 1.0.0.1 / TODO: <公司名稱> | 「dexin」辅助工具（用途未验证） |
| `MonitorDetect.exe` | 617.4 KB | 1.0.0.6 / ASUS / MonitorDetect | 显示器/设备检测辅助 |
| `config.ini` | 2.8 KB | — | PID ↔ 产品目录映射，`Count = 50` |
| `MD5.ini` | 71.3 KB | — | 校验清单 |
| `profile.ini` | 49 B | — | `[SPATHA] index=0 keylog=0`、`[EVOLVE] index=0`（历史机型遗留） |
| `speed.ini` | 848 B | — | 18 个 PID 的 DPI Low/Mid/High 档位表，如 `[17] PID=6871 Low=9 Mid=7 High=5` |
| 45 个型号子目录 | 638 文件 / 1.75 MiB | — | 见第 3.3.1 节 |

**`config.ini` 关键条目**（`Count = 50`，P520 在第 44 条）：

```ini
[44]
Wireless_PID=6871
BL_PID=6873
Folder Name = P520
```

- **P520 没有自己的子目录**：`Folder Name = P520` 指向的目录不存在（45 个目录中无 `P520`）。P520 走 `View\p520` 页面 + SDK 内置能力（`speed.ini [17] PID=6871` 反而有），而不是老机型的 `MouseSDK\<Folder>\*.xml` 默认模板。
- `BL_PID=6873` 解释了「6873 页面」的存在感：它是 P520 的蓝牙 PID，注册表有 `ArmouryHttp\6873`，但 `View\6873` 未安装（见第 7 节）。
- 同时存在 `PID` / `Wireless_PID` / `BL_PID` 三种键：同一产品线以「有线/2.4G/蓝牙」三个 PID 归并到一个 `Folder Name`。

#### 3.3.1 45 个型号子目录（638 文件 / 1.75 MiB）

| 目录 | 文件 | 体积 | 目录 | 文件 | 体积 |
|---|---:|---:|---|---:|---:|
| Spatha | 24 | 104 KB | ImpactII Punk | 13 | 34 KB |
| P707 | 21 | 82 KB | P309 | 13 | 34 KB |
| P518 / P709 / P711 / P713 / ROG CHAKRAM X | 21 | 58 KB | Tuf Gaming M5 | 13 | 34 KB |
| P714 / P715 / P716 | 20 | 58 KB | Evolve | 13 | 34 KB |
| Gladius III / Gladius III Wireless | 20 | 57 KB | GladiusII Wireless | 13 | 34 KB |
| Common | 13 | 40 KB | GladiusII Origin COD | 13 | 34 KB |
| GladiusII Origin PNK | 13 | 36 KB | Carry | 13 | 34 KB |
| Chakram Core / P306 / P307 / P310 / P513 / P517 / P509 | 13 | 35 KB | P308 | 13 | 34 KB |
| GladiusII Origin / Impact | 14 | 35 KB | Pugio | 13 | 34 KB |
| Chakram | 13 | 35 KB | ImpactII White | **12** | 34 KB |
| ImpactII Wireless | 13 | 35 KB | GladiusII | 13 | 34 KB |
| PugioII | 13 | 35 KB | MD200 / MD300 / Tuf Gaming M3 | **5** | 12 KB |
| ImpactII gundam / GladiusII Core / ImpactII | 13 | 34 KB | Gladius / Sica | **4** | 11 KB |

**抽样 3 个**

1. `ImpactII Wireless\`（13 文件 / 35 KB）：`button1..3.xml`(4,898 B)、`calibration1..3.xml`(477 B)、`effect1..3.xml`(5,756–5,786 B)、`performance1..3.xml`(633 B)、`profile.xml`(359 B)——3 套配置模板。
2. `P715\`（20 文件 / 58 KB）：`button1..5 / calibration1..5 / effect1..5 / performance1..5`，无 `profile.xml`——5 套配置。
3. `Common\`（13 文件 / 40 KB）：`button1..3 / calibration1..3 / effect1..3 / performance1..3 / profile.xml`，其中 `button1.xml` 9,798 B（约为机型版两倍，推断为全按键模板）。

**XML schema（`data_name key` 即 SDK functionNumber）**

```xml
<?xml version="1.0" encoding="UTF-8" standalone="no" ?>
<root>
  <device_type key="1">              <!-- 1 = Mouse -->
    <device key="6471">              <!-- 机型 PID，此处 ImpactII Wireless -->
      <data_name key="2">            <!-- 1=KEY_CHANGE 2=PERFORMANCE 3=EFFECT 4=CALIBRATION -->
        <dpi_1>3</dpi_1><dpi_2>7</dpi_2><dpi_3>15</dpi_3><dpi_4>31</dpi_4>
        <angle_snapping>0</angle_snapping><accelerate>0</accelerate><decelerate>0</decelerate>
        <polling_rate>3</polling_rate><button_response>2</button_response>
        <angle_tuning>-1</angle_tuning>
      </data_name>
    </device>
  </device_type>
</root>
```

- `buttonN.xml`：`button_function_1..27` + `button_N_key_1..3`，键值如 `16`(左键)/`272`(右键)/`528`(中键)/`784,1040`(侧键)，按 512 递增（推断 `(HID usage << 4) | 功能位` 或厂商自定义键码）。
- `effectN.xml`：多 `<area key="0..n">`，字段 `effect_id/brightness/speed/direction/random/color_count_1(r,g,b)`。
- `calibrationN.xml`：`calibration_function/material/height/result`。
- `profile.xml`：`device_path`、`profile_count`、`profile_N_lauch`、`profile_N_link`（`lauch` 为原始拼写）。

### 3.4 `dll\Schedule\` 与 `dll\Tools\`

| 文件 | 大小 | 版本 | 说明 / 字符串证据 |
|---|---:|---|---|
| `Schedule\AsTaskSched.dll` | 25.5 KB | FileVersion `0, 1, 0, 4` / ProductVersion `1, 0, 0, 1` / ASUS / AsTaskSchedule | 导出 `RegisterScheduledTask`、`DeleteScheduleTask` |
| `Schedule\AsTaskSchedArg.exe` | 659.3 KB | 无版本资源 | 含 `/change /tn "ASUS\Framework Service" /DISABLE`、`schtasks.exe`、`@@Taskschedexe@Initialize/Finalize` |
| `Schedule\KillProc_framewoork.exe` | 3,364.4 KB | 1.0.0.11 / SdkSetup | 按 ini 杀进程；PDB 名 `KillProc_framewoork.pdb`；清单 requireAdministrator |
| `Schedule\RegSchdTask.exe` | 795.0 KB | 1.0.0.1 / ASUSTeK Computer Inc. | 调 AsTaskSched.dll 注册/删除任务；字符串 `Taskname: %s` |
| `Tools\AsTaskSched.dll` | 28.3 KB | 同上（体积不同，推断不同构建） | — |
| `Tools\RegSchdTask.exe` | 797.8 KB | 1.0.0.1 | 同上 |
| `Tools\RegSchdTask.exe.manifest` | 1.1 KB | — | requireAdministrator |

### 3.5 `dll\UnifyDongleSDK\`

| 文件 | 大小 | 版本 / 公司 | 说明 |
|---|---:|---|---|
| `AC_UnifyDongleSDK.dll` | 1,216.8 KB | 1.0.0.10 / ASUSTek Computer Inc. | 统一接收器 SDK，`config.ini Type ID=21` |
| `MD5.ini` | 0.2 KB | — | `[Device] Name=UnifyDongleSDK`，`Value=1742493003 / InstalledValue=1742493003 / Matched=0` |

### 3.6 HAL 不在这四个目录（重要）

`AacMouseHal_x86.dll` **不在** `ArmouryDevice` 内，而在独立 MSI 路径：

```text
C:\Program Files\ASUS\Aac_Mouse\
├─ AacMouseHal_x64.dll   2,849,120 B
└─ AacMouseHal_x86.dll   3,361,120 B
```

- 卸载项证据：`ASUS Mouse HAL 1.2.1.18`，MSI `{5C648405-6B50-4AB9-8EB5-7FC289015151}`；另见 `C:\ProgramData\Package Cache\{b67124ae-a247-4249-a5ea-3107fb09bba9}\AacMouseSetup.exe`（614,616 B）+ `state.rsm`。
- 所以「安装目录规模」只统计 4 个 ASUS 子目录时，HAL（约 5.9 MiB）与 ProgramData 数据面都在其外——也是第 1 节体积对不上的可能来源（推断）。

---

## 4. `modules\`：框架路由插件（不是原生桥）

| 文件 | 大小 | 内容签名 |
|---|---:|---|
| `modules\mouse\index.js` | 81.5 KB（83,428 字符） | webpack 产物，导出 `t.default={router:i.default,deviceType:u.MOUSE}`（`MOUSE="1"`） |
| `modules\mouse\index.js.LICENSE.txt` | 0.1 KB | 许可证注释 |
| `modules\mouse\MD5.ini` | 0.3 KB | `Name=mouse`；`FileName=D:\Workspace\ArmouryCrate\AC_Backend_Mouse\build\mouse\mouse\index.js`；`Value=2304261245` |
| `modules\unifyDongle\index.js` | 23.3 KB（23,896 字符） | 导出 `{router, deviceType:u.UNIFYDONGLE}`（`UNIFYDONGLE="21"`） |
| `modules\unifyDongle\MD5.ini` | 0.2 KB | `Name=unifyDongle`；`D:\Workspace\ArmouryCrate\AC_Backend_UnifyDongle\...\index.js`；`Value=3240146441` |

**加载机制（更正背景里的判断）**：不是「ac_node_addon 原生插件桥」，而是框架自己 require() 的 Express 路由模块。asar `.webpack\main\index.js` 的 checkModules 反编译可读：

```js
var i = path.resolve(pathMapping.proj + "/modules");
for (const s of fs.readdirSync(i)) {
  var p = path.resolve(i + "/" + s);
  var d = require(p).default || {};
  var { deviceType, deviceName, router, shouldPreload, isLibrary } = d;
  if (!d.isLibrary) {
    if (deviceName) setDeviceName(deviceType, deviceName);
    if (!router) throw errorCode.PLUGIN_FORMAT_ERROR;   // F-1016
    routers[deviceType] = router(ctx);                 // 按 deviceType 挂载
    if (shouldPreload) n.push(deviceType);
  }
}
```

- 两个 modules 的 deviceType 恰好等于 `dll\config.ini` 的 Type ID（1 = Mouse，21 = UnifyDongle）；`checkServiceAvailable` 又用 URL 里的 `deviceType/foreignLib` 直接索引 `routers[deviceType]`。
- `modules\mouse\index.js` 只 `require("crypto")` 与 `require("path")`（无原生绑定），实质工作是生成/解析 SDK 命令 XML 与 HTTP 信封：内含 `functionNumber` 全表（`KEY_CHANGE:"0", PERFORMANCE:"1", EFFECT:"2", CALIBRATION:"3", POWER:"4", PROFILE:"5", LOAD_DATA:"6", SYNC:"7", FRAME:"8", KEY_LOG:"9", QUERY:"10", WRITE_FILE:"11", DELETE_FILE:"12", EVENT:"13", DIAL_SETTING:"15", JOYSTICK_SETTING:"16", FW_UPDATE:"17", DEVICE_RE_PLUGIN:"18"`）、`commandCode`（`DEVICE_PLUGGED:"30001"` 等）、`errorCode`（`S-1000…S-1011 / F-1010…F-1041`）。
- 与 ac_node_addon 的真实关系：它由**框架主进程**（asar `sdk.js`）`require("ac_node_addon")` 使用，做设备枚举 / 蓝牙 VID-PID 解析（第 5 节），与 `modules\` 无直接调用关系。

---

## 5. `resources\app.asar`（28 MB）与 `app.asar.unpacked\node_modules`

### 5.1 asar 本体

| 项 | 值 |
|---|---|
| 文件 | `C:\Program Files (x86)\ASUS\ArmouryDevice\resources\app.asar` |
| 大小 | **28,374,473 B**（27.06 MiB / 28.37 MB） |
| 头部 JSON | 207,490 B；数据基址 = `8 + headerPickleSize` = 207,508（`uint32[12]` 起始处为 JSON 内容长度） |
| 产品清单 | `package.json`：name `asus_framework`，version `4.2.2.8`，main `.webpack/main`，author ASUSTeK，license MIT |

**asar 顶层条目（7 项）**

| 条目 | 文件数 | 体积 | 说明 |
|---|---:|---:|---|
| `.webpack` | 19 | 6.81 MB | 真正的应用代码（main + renderer，含 sourcemap） |
| `.sign` | 6 | 14.74 MB | 签名副本：`addon/ac_node_addon.node`(463,704 B) + `electron/ffmpeg|libEGL|libGLESv2|vk_swiftshader|vulkan-1.dll` |
| `node_modules` | 756 | 5.34 MB | 106 个包（第 5.2 节） |
| `icons` | 2 | 0.73 MB | `icon.icns` / `icon.ico`（各 381,142 B） |
| `package.json` | 1 | 3,417 B | 见上 |
| `forge.config.ts` | 1 | 0.01 MB | Electron Forge 构建配置 |
| `babel.config.js` | 1 | ~0 | Babel 配置 |

**`.webpack` 清单与用途**

| 文件 | 大小 | 用途 |
|---|---:|---|
| `mainindex.js` | 1,421,460 B | 框架主进程：HTTP/HTTPS 服务、checkModules、utilityProcess.fork(sdk.js)、launchWindow |
| `mainsdk.js` | 254,574 B | **SDK 宿主进程**：以 `--deviceType= --targetSDKPath= --projPath=` 启动，负责 LoadLibrary + ExecuteFunction |
| `mainindex.js.map` / `sdk.js.map` | 3,986,174 / 904,883 B | sourcemap（保留源码结构，逆向主入口） |
| `renderer	itleBarindex.js` | 153,072 B | 标题栏渲染进程 |
| `rendererwindowpreload.js` | 2,302 B | 预加载桥 |
| `rendererwindowindex.js` | 134 B | 空壳入口 |
| `rendererwindowindex.html` / `titleBarindex.html` | 579 / 184 B | 页面骨架 |
| `back_ic_{h,n,p}.svg`、`icon.png` | 631–3,767 B | 返回按钮三态 / 图标 |

### 5.2 `node_modules`：106 个包

- asar 内 `node_modules` 顶层 133 条目，展开 scope 后 **106 个包**，756 文件 / 5.34 MB。
- 体积前列：`node-forge`(54 文件/1.47 MB)、`.package-lock.json`(0.63 MB)、`ac_node_addon`(6/0.52 MB)、`@img/sharp-win32-ia32`(0.33 MB)、`iconv-lite`、`sharp`、`xml2js`、`ws`(16/0.12 MB)、`express`(25/0.11 MB)、`log4js`、`uuid`、`streamroller`、`multer`、`pidusage`、`selfsigned`、`helmet`、`cors`、`minimist`、`mkdirp`。

**与硬件交互相关的包（逐个核对）**

| 包 | 是否在 asar | 说明 |
|---|---|---|
| `ac_node_addon` | 是（+ unpacked 内 .node） | **唯一原生硬件枚举模块**；.node 含 `SetupDiGetDeviceRegistryPropertyW`、`SetupAPI.dll`、`CreateFileW`、`[BtDeviceDetector][GetVidPidFromBthAddress]`、`vidpid`、`setRegistry` |
| `serialport` / `usb` / `node-hid` / `node-usb` | **均不存在** | 串口 / HID 访问不在 Node 层 |
| `koffi`（FFI） | asar 内无，**仅 unpacked** | 见第 5.3 节；未被 package.json dependencies 引用，推断为构建期/上游残留 |
| `sharp` / `@img/sharp-win32-ia32` | 是 | 图片处理（`libvips-42.dll` 17.9 MB 在 unpacked） |
| `selfsigned` / `node-forge` | 是 | **运行期生成自签 HTTPS 证书**（`generate([], {algorithm:"sha256", days:365})`） |
| `express` / `helmet` / `cors` / `ws` / `multer` / `log4js` / `xml2js` / `pidusage` / `uuid` | 是 | 本地 HTTP/WS 服务、日志、XML 命令序列化、进程占用采样 |

> 结论：**硬件读写完全不走 Node 原生 HID**。Node 层只做「设备枚举 + HTTP/WS 服务 + XML 编解码」，HID 帧由 `ArmouryMsSDK.dll`（经 `AacMouseHal_x86.dll`）发出。

### 5.3 `app.asar.unpacked\node_modules`：23 文件 / 81.04 MB

```text
app.asar.unpacked
ode_modules├─ ac_node_addonuildRelease│   ├─ ac_node_addon.node        453 KB   ← 原生模块实体（asar 内 .sign 有 463,704 B 副本）
│   ├─ config.ini                 44 B    "[UnifyDongle] Count=2 / 0=6862 / 1=6864"
│   ├─ UnifyDeviceDetector.dll   123 KB
│   └─ UnifyScanner.dll          344 KB
├─ @imgsharp-win32-ia32lib│   ├─ libvips-42.dll          17,914 KB
│   ├─ libvips-cpp.dll            329 KB
│   └─ sharp-win32-ia32.node       338 KB
└─ koffiuildkoffi<平台>koffi.node  ×17 平台
    darwin_arm64 3,742KB · darwin_x64 4,061KB · freebsd_arm64 4,879KB · freebsd_ia32 4,088KB
    freebsd_x64 5,326KB · linux_arm32 3,590KB · linux_arm64 4,718KB · linux_ia32 4,047KB
    linux_riscv64 3,401KB · linux_x64 5,248KB · musl_x64 5,096KB · openbsd_ia32 4,155KB
    openbsd_x64 5,329KB · win32_arm64 1,886KB · win32_ia32 1,632KB · win32_x64 2,286KB
```

`ac_node_addonuildReleaseconfig.ini`（44 B，说明哪些 PID 视为 Unify Dongle）：

```ini
[UnifyDongle]
Count = 2
0 = 6862
1 = 6864
```

> `koffi` 的 17 个平台二进制里 Windows x86 Electron 只用得到 `win32_ia32koffi.node`（1,632 KB），其余 15 个合计约 **74 MB 纯冗余**（推断：forge 的 unpack 规则过宽，未按平台裁剪）。

---
## 6. `Firmware\`（已安装）与安装包对比

### 6.1 已装文件清单

```text
ArmouryDevice\Firmware\
├─ 6862\         (OMNI / Unify Dongle 接收器)
│   ├─ config.ini                    401 B
│   ├─ Dependence.ini                 95 B
│   ├─ FW_Update_Tool_Unify_Dongle.exe 37,736 B  4.0.0.1  FW_Update_Tool_KB
│   ├─ InterruptTransfer.dll         82,640 B  1.0.111.11111
│   └─ FW\
│       ├─ HidInterruptHandle.dll    93,600 B  1.0.4.0  ASUSTeK computer Inc.
│       ├─ OMNI_RECEIVER_V06_00_25.bin 253,952 B
│       └─ peripheral_fwu_pro.exe   286,568 B  1.0.0.7  ASUS / peripheral fw pro updater
├─ 6871\         (P520 鼠标本体)
│   ├─ Dependence.ini                 89 B
│   ├─ HidInterruptHandle.dll        93,600 B  1.0.4.0
│   ├─ InterruptTransfer.dll         90,672 B  1.0.2.1
│   ├─ My_FW_Update_P520_Mouse.exe   57,192 B  2.0.0.2  My_FW_Update
│   ├─ P520_MOUSE_V03_00_02.bin     520,192 B
│   ├─ P520_MOUSE_V04_00_02.bin     520,192 B
│   ├─ peripheral_fwu.exe           279,400 B  1.0.0.5  ASUS / peripheral fw pro updater
│   └─ version.ini                      211 B
└─ Tools\
    ├─ Config.ini                        42 B
    ├─ result.ini                        55 B
    ├─ ShowConfig.ini                    22 B
    ├─ GetDeviceVersion.exe         127,952 B  1.0.0.4  ASUS / GetDeviceVersion
    └─ ROGWeChat.exe              3,186,024 B  1.0.0.2  ROGWeChat（渠道二维码工具）
```

**关键 ini 原文**

```ini
; 6871\version.ini
[Version]
Vid=2821            ; 0x0B05 ASUS
Pid=6862            ; 接收器 PID
BootloaderPid=6863
MCUCount=1
Complete=4.00.02
Code=255
[MCU_1]
CMD=s 1ACE 1ACF 112 248 FF01 FF01 1
Version=P520_MOUSE_V04_00_02.bin
CMD_2=1AD7 UA:y \npause
Number=0
```

```ini
; 6862\config.ini（节选）
[USB Info]
Vid = 0x0b05
Pid = 0x1ACE
Usage = 0x01
UsagePage = 0xff02
Device Type = 0     ; 0=Unify Dongle 1=Keyboard 2=Mouse
[FW Info]
Bin Name = OMNI_RECEIVER_V06_00_25.bin
Cmd 1 = m 1ACE 1ACF 48 64 FF02 FF01 4
Version = 6.00.25
Executable file = peripheral_fwu_pro.exe
[Parameters]
Success Code = 255
Ignore Error = 12
Wait for ready = 6000

; 6871\Dependence.ini
[Config]
HTML=0
SDK=0
HAL=0
FrameworkMSPlugin=0
cmd=My_FW_Update_P520_Mouse.exe -s

; Tools\Config.ini   /   Tools\result.ini   /   Tools\ShowConfig.ini
[1] Type=1 Pid=6871      [1] Pid=6871 FWDGVersion=7.00.08      [ShowDialog] Show=1
```

> `CMD=s 1ACE 1ACF 112 248 FF01 FF01 1` 与 `Cmd 1 = m 1ACE 1ACF 48 64 FF02 FF01 4` 是 HID 特性报告命令模板（`1ACE` = 接收器 PID，末尾数字为报告长度），与 HAL 的 65 字节报告格式一致。

### 6.2 与安装包对比（15 个共有文件全部逐字节一致）

| 相对路径 | 已装 | 安装包 | SHA-256 前 16 | 结论 |
|---|---:|---:|---|---|
| `6862\config.ini` | 401 | 401 | — | 一致 |
| `6862\Dependence.ini` | 95 | 95 | — | 一致 |
| `6862\FW\HidInterruptHandle.dll` | 93,600 | 93,600 | — | 一致 |
| `6862\FW\OMNI_RECEIVER_V06_00_25.bin` | 253,952 | 253,952 | `bd8e4bd397b61c0a` | 一致 |
| `6862\FW\peripheral_fwu_pro.exe` | 286,568 | 286,568 | — | 一致 |
| `6862\FW_Update_Tool_Unify_Dongle.exe` | 37,736 | 37,736 | — | 一致 |
| `6862\InterruptTransfer.dll` | 82,640 | 82,640 | — | 一致 |
| `6871\Dependence.ini` | 89 | 89 | — | 一致 |
| `6871\HidInterruptHandle.dll` | 93,600 | 93,600 | — | 一致 |
| `6871\InterruptTransfer.dll` | 90,672 | 90,672 | — | 一致 |
| `6871\My_FW_Update_P520_Mouse.exe` | 57,192 | 57,192 | — | 一致 |
| `6871\P520_MOUSE_V03_00_02.bin` | 520,192 | 520,192 | `4b094295b508110f` | 一致 |
| `6871\P520_MOUSE_V04_00_02.bin` | 520,192 | 520,192 | `c1104f10058f71f4` | 一致 |
| `6871\peripheral_fwu.exe` | 279,400 | 279,400 | — | 一致 |
| `6871\version.ini` | 211 | 211 | — | 一致 |

- 安装包侧路径：`...\v1.0.2.2\Firmware\Bin\Firmware\<同上>`（该目录 23 文件）。
- 安装包多出的 8 个：`Bin\FWUpdateSetup.exe`、`Bin\Setup.ini`、`Tools\FWUpdateSetup.exe`、`Tools\Setup.ini` 是安装器本体（不落安装目录）；`Tools\Tools\cpprest141_2_10.dll` 与 `Tools\Tools\MsFWUpdate.dll` 被装到 `ArmouryDevice\` 根目录（同名文件体积略异：445,792 vs 435.3 KB，推断不同构建）。
- 已装多出：`Firmware\Tools\Config.ini / result.ini / ShowConfig.ini`（`GetDeviceVersion.exe` 运行期生成）。

---

## 7. `locales\` 与 `View\` 规模

### 7.1 locales

- 只有 **1 个文件** `en-US.pak`：`C:\Program Files (x86)\ASUS\ArmouryDevice\locales\en-US.pak`，**451,080 B**。
- 其余 50+ 个 Chromium 语言包被裁剪 → 界面语言由设备页自带资源（`View\*\resources\`、`dll\ArmourySocketServer\device_language\`）提供。

### 7.2 View

| 页面目录 | 文件数 | 体积 | config.ini 关键字段 | 定位 |
|---|---:|---:|---|---|
| `View\6871` | 619 | 21.43 MiB | `ArmourySocketServer=true`、`DeviceType=1`、`ReloadS3=1`、`DG=6871`、`BT=6873` | **P520 2.4GHz 主页面**（含 `service.js` 621,724 B → 设备服务入口） |
| `View\p520` | 111 | 12.14 MiB | `ArmourySocketServer=true`、`DeviceType=1`、`ReloadS3=1` | **P520 通用页面**（无 `service.js`） |
| `View\omni` | 89 | 7.66 MiB | `ArmourySocketServer=false`、`DeviceType=21` | Omni 接收器页面（含 `HomePage.exe` 5,278,568 B、`Shortcut.exe` 1,562,984 B、`shortcut.lnk`、`Setup.ini`） |
| `View\6862` | 123 | 1.68 MiB | `ArmourySocketServer=false`、`DeviceType=21` | Omni 接收器（6862 变体，页面更轻） |
| **合计** | **942** | **44,997,726 B = 42.91 MiB** | — | 与背景「942 文件 / 42.9MB」吻合 |

- **`View\6873` 不存在**。6873 只是 P520 的蓝牙 PID（`MouseSDK\config.ini BL_PID=6873`、`View\6871\config.ini BT=6873`），注册表也有 `ArmouryHttp\6873`（`DisplayName=ROG STRIX IMPACT III WIRELESS (Bluetooth)`、`Version=4.05.38`），但页面文件未安装到本机。
- 每页都带 `MD5.ini`（如 `View\6871\MD5.ini` 112,053 B）、`index.html`（357 B 骨架）与若干 `*-bundle-*.js/.css` 分块。`View\6871\service.js` 内嵌 P520 设备描述：`productName:"P520", deviceType:"1", multiModelNumber:["6871","6873"], profileCount:5, deviceZH:"ROG 影刃 3 无线版", theme:"rog", deviceProps:{isWireless:!0,isAuraSyncEditable:!0,...}`。

---

## 8. 运行时数据面 `C:\ProgramData\ASUS`（只读）

### 8.1 顶层结构（57 条目 / 5 个顶层目录）

```text
C:\ProgramData\ASUS\
├─ ALL\ACLoader\                        AcLoader / ROGLSLoaderUI 日志
│   ├─ ACSE.ini                         112 B   [UWP] ArmouryCrate= / [Main] IsROGConsoleProduct=0
│   ├─ AsusROGLSLService.log            0 B
│   ├─ ROGLSLoaderUI_App_20261006170553.log            344 B
│   ├─ ROGLSLoaderUI_ACInstallerDialog_20261006170553.log  733 B
│   ├─ ROGLSLoaderUI.Util_Util_20261006170553.log      246 B
│   └─ UWPInstaller.log                 5,099 B
├─ ArmourySDK\MouseSDK\P520\            ★ 运行时设备配置(8.2)
├─ Framework\MouseSDK\P520\             ★ 框架侧 profile 快照(8.3)
├─ MAFWUpdate\                          ★ 固件更新状态(8.4)
└─ ARMOURY CRATE Diagnosis\             诊断日志(8.5)
```

无其它 ASUS 相关目录。

### 8.2 `ArmourySDK\MouseSDK\P520\` 全文件与内容

| 文件 | 大小 | 内容要点 |
|---|---:|---|
| `performance1.xml` | 725 B | `device key="6871"`、`data_name key="2"`；`dpi_1..4 = 7/15/31/75`、`angle_snapping=0`、`accelerate=0`、`decelerate=0`、`polling_rate=3`、`button_response=-1`、`angle_tuning=3`、`high_performance_mode=-1` |
| `calibration1.xml` | 478 B | `data_name key="4"`；`calibration_function=1`、`calibration_material=-1`、`calibration_height=0`、`calibration_result=-1` |
| `effect1.xml` | 5,798 B | `data_name key="3"`；`all_area_flag=1`，多 `<area key="0..n">`；统一 `effect_id=4`、`brightness=1`、`speed=1`、`direction=-1`、`color=(255,0,0)` |
| `202410050572\button1.xml` | 12,542 B | **UTF-16LE（BOM FF FE）+ CRLF**；`device key="6871"`、`data_name key="1"`，共 **27 组** `button_function_N / button_N_key_1..3`；默认 `16/272/528/784/1040/273/17` |

> 该目录**没有顶层 `button1.xml`**：按键表只存在于 `<设备 SN>\` 子目录（按 SN 分档）。SN `202410050572` 与框架日志 `getConnectUsbDeviceList` 的 `deviceSN` 一致。

### 8.3 `Framework\MouseSDK\P520\`：框架侧 profile 快照

三个文件同构：`<function key="11">`（WRITE_FILE）内 `<file_data>` 是 **Base64 → URL 编码的 JSON**。

| 文件 | 大小 | 解码后 | 内容摘要 |
|---|---:|---:|---|
| `config_202410050572.xml` | 2,128 B | 729 B | `deviceSN="202410050572"`；`profileList` 5 条（全 `FIRMWARE_PROFILE`，名「配置文件 1..5」）；`swProfileList:[]`；`currProfile.id="1"`、`dpiNotificationStatus:true`；`hasEverSyncAllProfile:true`、`isFirstLoginDone:true`；`lighting.customColors.color` 8 空槽、`auraSyncBrightness:"2"` |
| `fp_202410050572_1_config.xml` | 4,009 B | 1,561 B | 配置 1 完整快照：`button` 7 键；`performance`（dpi 7/15/31/75、`currentDPI=4`、`dpiNumber=4`、`pollingRate=3`、`pollingRate_Booster=6`、`angleTuning=3`、DPI 颜色 `255,0,0 / 193,0,255 / 0,61,255 / 49,255,0`）；`lighting`（`effectID=4`、`areaKey=3`、`Single`、rgb 255,0,0）；`calibration`（`calibrationProcessing:true`、`calibrationMaterialUI:"255"`）；`power`（`timeToSleep=0`、`lowPowerToShine=2`、`powerSavingMode=1`、`dongleParingSwitch=-1`、`isHighPower:false`） |
| `fp_202410050572_1_previousLighting.xml` | 1,167 B | 334 B | `previousLighting."2"`：`effectID=2`、`colorType=Pattern`、`brightness=2`、`default:true`、`areaKey=3`——用于灯效切换后恢复上一次 |

> 数据流推断：页面 → `Framework\...`（结构化 JSON 快照，含 UI 状态如 `dpiNotificationStatus`）→ `ArmourySDK\MouseSDK\P520\`（SDK 命令 XML，纯设备参数）→ SDK ExecuteFunction。

### 8.4 `MAFWUpdate\`：固件更新状态

| 文件 | 大小 | 关键内容 |
|---|---:|---|
| `FWUpdateConfig.ini` | 907 B | `[ROG STRIX IMPACT III WIRELESS]`：`Mode=DG+BT`、`Mode_VIDPID=0B051AD9,0B051AD8,0B051AD7`、`UDG_VIDPID=0B051ACE`、`UDG_Service_FWVersion=7.00.08`、`USB_VIDPID=0B051AD7`、`USB_Service_FWVersion=4.00.02`、`UDG_FWPackage=https://dlcdn-rogboxbu2.asus.com/pub/ASUS/APService/Gaming/SYS/ROGS/40703-LZWD4C-....zip`、`USB_FWPackage=...21299-OTELNN-....zip`、`USB_NeedUpdate=false`、`UDG_NeedUpdate=false`、`ConnectBooster=false`、`UDG_DownLoadFolderName=...\0B051ACE\FW_Update_Tool_Unify_Dongle_v70008` |
| `SettingConfig.ini` | 261 B | `token=0`、`Times=2026/10/18 02:26:54`、`GetData=2026/10/17`、`CountryCode=CN`、`limited=<JWT>`（载荷含 username `AuraEditor`、user_id 2；**值已省略，勿外传**） |
| `ROG STRIX IMPACT III WIRELESS\` | 空目录 | 固件下载缓存位（当前为空，因 `*_NeedUpdate=false`） |

> 与注册表互证：`ArmouryHttp\6871` 的 `FWVersion=4.00.02` / `FWDGVersion=7.00.08` 完全相同，`Firmware\Tools\result.ini` 的 `FWDGVersion=7.00.08` 也一致。

### 8.5 `ARMOURY CRATE Diagnosis`：目录结构与日志格式

```text
ARMOURY CRATE Diagnosis\
├─ AacMouseSetup\                        (空)
├─ ArmouryCrateGearInstall\
│   ├─ 2026-09-30_00.log    31,090 B
│   └─ 2026-10-18_00.log   178,350 B   ★ 本次安装
├─ Framework\
│   ├─ 81197c2d02b3a7c54cbd498ce55a73c8\   Framework.00.2026-10-17.log 4,723 / Service.01 79 / View.01 69 / View.21 161
│   ├─ 9dd4e461268c8034f5c8564e155c67a6\   Framework.00.2026-10-17.log 1,399
│   └─ ee11cbb19052e40b07aac0ca060c23ee\   ★ 当前实例
│        Framework.00.2026-10-18.log   30,106 B  (592 行)
│        Framework.6862.2026-10-18.log    163 B
│        Framework.6871.2026-10-18.log    185 B
│        Framework.ArmourySDK.2026-10-18.log   0 B
│        Service.01.2026-10-18.log        420 B
│        View.01.2026-10-18.log           745 B
│        View.21.2026-10-18.log            83 B
├─ MAFWUpdate\                          (8.4)
├─ MainSDK\ArmourySwAgent\              (空)
├─ MainSDK\SDK\                          (空)
├─ MouseSDK\
│   ├─ 2026-10-17.log   130,652 B  (2,421 行)
│   └─ 2026-10-18.log   347,972 B  (7,028 行)
├─ UnifyDongleSDK\                      (空)
└─ UnifyScanner\                        (空)
```

> `Framework\<32位hex>\` 三个值（`81197c2d…`、`9dd4e461…`、`ee11cbb1…`）为实例/会话标识，命名算法未验证。

**日志格式**

| 日志族 | 行格式 | 样例 |
|---|---|---|
| `Framework.00.*.log` | `[YYYY/MM/DD HH:mm:ss.mmm][T\|E\|W] [Component] message` | `[2026/10/18 00:02:40.165][T] [Server] listening on port 1042, pid is 5560` |
| `Framework.<pid>.log` | 同上，前缀为路由请求 | `[2026/10/18 01:48:25.446][E] GET /6871?dongleSN=S8MPGDD01ZX7&deviceSN=202410050572&isElectron=1&session=... - SDK config not defined device type 6871.` |
| `View.NN` / `Service.01` | 同上，以 `[E]` 为主 | `[2026/10/18 01:33:00.847][E] [E][Device Page] error: Error: H-4004.` |
| `MouseSDK\YYYY-MM-DD.log` | `YYYY-MM-DD HH:mm:ss,mmm [ArmouryMsSDK][3.01.48]-[ArmouryMsSDK][Tag]msg`（逗号毫秒） | `2026-10-18 00:02:43,216 [ArmouryMsSDK][3.01.48]-[ArmouryMsSDK][startService]port 1042` |
| `ArmouryCrateGearInstall\*.log` | **UTF-16LE + CRLF**：`[YYYY-MM-DD HH:mm:ss.mmm][][pid] [安装器版本] 消息` | `[2026-10-18 02:07:33.766][][19668] [1.0.2.2]  Config file = ...\v1.0.2.2\Config.ini` |

**样例 1：`Framework.00.2026-10-18.log` 前 30 行**（一次完整启动）

```text
[2026/10/18 00:02:39.855][T] [Server] set log level as 3.
[2026/10/18 00:02:40.126][T] [Server] construct Server
[2026/10/18 00:02:40.134][T] [Server] initialize Server
[2026/10/18 00:02:40.165][T] [Server] listening on port 1042, pid is 5560
[2026/10/18 00:02:40.309][T] [Server] listening on port 1043, pid is 5560
[2026/10/18 00:02:40.309][T] [Server] Check modules.
[2026/10/18 00:02:40.310][T] [Server] Module loading: mouse
[2026/10/18 00:02:40.323][T] [Server] Module loading: unifyDongle
[2026/10/18 00:02:40.331][T] [Server] server initialization completed.
[2026/10/18 00:02:40.331][T] [DeviceManager] loadDeviceServices: ffc3b3b1-de7e-44fc-bb27-6d218b127474
[2026/10/18 00:02:40.332][T] [DeviceManager] getConnectedDevices
[2026/10/18 00:02:41.176][T] [DeviceManager] getConnectUsbDeviceList: [
    { "vidpid": "0B051ACE", "dongleSN": "S8MPGDD01ZX7" },
    { "vidpid": "0B051AD7", "deviceSN": "202410050572", "dongleSN": "S8MPGDD01ZX7" }
]
[2026/10/18 00:02:41.179][T] [DeviceManager] getConnectBtDeviceList: []
[2026/10/18 00:02:41.210][T] [Server] initialize Main SDK
[2026/10/18 00:02:42.305][T] [Server] SDK list count 13.
[2026/10/18 00:02:43.464][E] [DeviceManager] handleOnDeviceChanged: 6862 is not defined in the config.ini of SDK.
[2026/10/18 01:06:21.291][E] [Server] child-process-gone:
{
    "type": "GPU",
    "reason": "crashed",
```

- 注意：`0B051ACE` = 接收器（`UDG_VIDPID`），`0B051AD7` = P520 鼠标（`USB_VIDPID`），SN `202410050572`，dongle SN `S8MPGDD01ZX7`；`6862` 不在 `dll\config.ini` 的 Type ID 映射中，故报「未定义」。

**样例 2：`MouseSDK\2026-10-18.log` 前 25 行**

```text
2026-10-18 00:00:45,580 [ArmouryMsSDK][3.01.48]-[ArmouryMsSDK][ImmediActionEvent]PID:6871 is alive
2026-10-18 00:02:43,215 [ArmouryMsSDK][3.01.48]-[ArmouryMsSDK][HttpPort]get port
2026-10-18 00:02:43,216 [ArmouryMsSDK][3.01.48]-[ArmouryMsSDK][HttpPort]port 1042
2026-10-18 00:02:43,216 [ArmouryMsSDK][3.01.48]-[ArmouryMsSDK][startService]port 1042
2026-10-18 00:02:43,225 [ArmouryMsSDK][3.01.48]-{in} ExecuteFunction
2026-10-18 00:02:43,225 [ArmouryMsSDK][3.01.48]-+++++strInputXml+++++
2026-10-18 00:02:43,225 [ArmouryMsSDK][3.01.48]-<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<root>
  <device_type key="1">
    <device key="6871">
      <sn>202410050572</sn>
      <function key="6">
        <data_number>19</data_number>
        <file_name>-1</file_name>
        <profile_index>-1</profile_index>
      </function>
    </device>
  </device_type>
</root>
2026-10-18 00:02:43,226 [ArmouryMsSDK][3.01.48]------strInputXml-----
2026-10-18 00:02:43,227 [ArmouryMsSDK][3.01.48]-[ArmouryMsSDK]enter ArmouryMsSDK
```

**样例 3：一条完整 `function key="1"`（PERFORMANCE）写入链**（2026-10-18 02:08:15）

```text
... [ArmouryMsSDK][3.01.48]-<function key="1">
      <profile_index>1</profile_index><dpi_number>4</dpi_number>
      <accelerate>0</accelerate><decelerate>0</decelerate><angle_snapping>0</angle_snapping>
      <angle_tuning>3</angle_tuning><button_response>-1</button_response><polling_rate>3</polling_rate>
      <dpi_channel>4</dpi_channel>
      <color><dpi_1>255,0,0</dpi_1><dpi_2>193,0,255</dpi_2><dpi_3>0,61,255</dpi_3><dpi_4>49,255,0</dpi_4></color>
      <dpi_1>7</dpi_1><dpi_2>15</dpi_2><dpi_3>31</dpi_3><dpi_4>719</dpi_4>
... enter function 1 → [SetPerformance]enter SetPerformance
    [ParsePerformanceXml]no high performance mode / no dpi onoff / no dial mutipller / no dial sensitivity
    [SetPerformance]exit SetPerformance → function 1 succese
... 紧接 <function key="5"><profile_function>3</profile_function><profile_parameter>1</profile_parameter>
```

- 当日 `function key` 分布：`key=6`（LOAD_DATA）119 次、`key=9/10/13` 各 13 次、`key=1/5` 各 6 次——页面轮询以「读数据」为主。
- 原始拼写保留：`succese`、`mutipller`。

**样例 4：`ArmouryCrateGearInstall\2026-10-18_00.log` 前 30 行**（UTF-16LE 解码后）

```text
[2026-10-18 02:06:12.110][][4188] [1.0.0.0] Start Update ------------
[2026-10-18 02:06:12.112][][4188] [1.0.0.0]  Config file = D:\文档\deepseek-harness\default-workspace\Config.ini
[2026-10-18 02:06:12.113][][4188] [1.0.0.0]   B4 GetSocketServerPath 
[2026-10-18 02:06:12.114][][4188] [1.0.0.0]   Program.szFrameworkPath = C:\Program Files (x86)\ASUS\ArmouryDevice\dll\ArmourySocketServer
[2026-10-18 02:06:12.221][][4188] [1.0.0.0]   execute  ArmourySocketServer.exe Done. 
[2026-10-18 02:06:12.221][][4188] [1.0.0.0]  ------------ End Update
[2026-10-18 02:07:33.765][][19668] [1.0.2.2] Start Update ------------
[2026-10-18 02:07:33.766][][19668] [1.0.2.2]  Config file = ...\ROG STRIX IMPACT III WIRELESS v1.0.2.2\Config.ini
[2026-10-18 02:07:33.767][][19668] [1.0.2.2]  Install file (Item_Path1) = Tools
[2026-10-18 02:07:33.768][][19668] [1.0.2.2]  Install file (Item_Name1) = vc_redist.x64.exe
[2026-10-18 02:07:33.768][][19668] [1.0.2.2]  Install file (Item_Param1) = /quiet /norestart
[2026-10-18 02:07:35.346][][19668] [1.0.2.2]  Install file (Item_Path2) = Tools
[2026-10-18 02:07:35.347][][19668] [1.0.2.2]  Install file (Item_Name2) = vc_redist.x86.exe
[2026-10-18 02:07:36.546][][19668] [1.0.2.2]  Install file (Item_Path3) = Framework
[2026-10-18 02:07:36.547][][19668] [1.0.2.2]  Install file (Item_Param3) = /install /quiet
[2026-10-18 02:07:36.548][][19668] [1.0.2.2]  Install file (Item_Ver3) = 4.2.2.8
[2026-10-18 02:07:36.549][][19668] [1.0.2.2]  RegKey : ArmouryDevice\ RegVer : 4.2.2.8
[2026-10-18 02:07:36.551][][19668] [1.0.2.2] [VersionCompare] can't install XXX.
[2026-10-18 02:07:36.553][][19668] [1.0.2.2]  Install file (Item_Path4) = SDK_HAL\FWMSPlugin
[2026-10-18 02:07:36.554][][19668] [1.0.2.2]  Install file (Item_Param4) = -s
[2026-10-18 02:07:36.555][][19668] [1.0.2.2]  Install file (Item_Ver4) = 2.4.3
[2026-10-18 02:07:36.557][][19668] [1.0.2.2]  RegKey : ArmouryFramework\mouse RegVer : 2.3.4
[2026-10-18 02:07:36.558][][19668] [1.0.2.2] [VersionCompare] can install OOO.
```

> 首段（1.0.0.0，PID 4188，`Config file = D:\文档\...\Config.ini`）与本机工作区路径一致——**推断**为在工作区内直接运行该安装器的调试痕迹，非产品自带行为。
> 第二段展示安装工具链顺序：`vc_redist x64/x86` → `FrameworkServiceSetup.exe /install /quiet`（4.2.2.8，已装则跳过）→ `SDK_HAL\FWMSPlugin\Setup.exe -s`（2.4.3）→ ……

**样例 5：其它小日志**

```text
; Framework\ee11cbb1...\View.01.2026-10-18.log（全 12 行，均 E 级）
[2026/10/18 01:33:00.847][E] [E][Device Page] error: Error: H-4004.
[2026/10/18 01:39:01.136][E] [E][Device Page] error: H-2005.
...
[2026/10/18 02:27:04.291][E] [E][Device Page] error: Error: H-4004.

; Service.01.2026-10-18.log（全 6 行）
[2026/10/18 01:32:21.079][E] [Device Service] 6871 socket server connection close.
...（同型 5 条）

; Framework.6871.2026-10-18.log（全 2 行）
[2026/10/18 01:48:25.446][E] GET /6871?dongleSN=S8MPGDD01ZX7&deviceSN=202410050572&isElectron=1&session=75bca231-... - SDK config not defined device type 6871.

; Framework.6862.2026-10-18.log（全 2 行）
[2026/10/18 01:48:33.699][E] GET /6862?dongleSN=S8MPGDD01ZX7&isElectron=1&session=337e8820-... - SDK config not defined device type 6862.
```

- `H-4004` / `H-2005` 是页面层错误码（未在 asar 的 errorCode 表内 → 推断为 `View\` 页面自定义）。
- `6871 socket server connection close` 是**拓扑关键证据**：框架把 `/6871` 的设备服务当作「socket server 连接」管理。

### 8.6 `ALL\ACLoader\` 日志

```text
; ROGLSLoaderUI_App_20261006170553.log
2026/10/6 17:05:53: SetDefaultDllDirectories = LOAD_LIBRARY_SEARCH_SYSTEM32,result = True
2026/10/6 17:05:53: e.Args[0] = -roglsloaderstart
2026/10/6 17:05:53: e.Args[1] = -win
2026/10/6 17:05:53: e.Args[2] = armourycrate
2026/10/6 17:05:53: e.Args[3] = -acinstaller
2026/10/6 17:05:53: enter armourycrate eula

; ROGLSLoaderUI.Util_Util_20261006170553.log
2026/10/6 17:05:54: [isSupportArmouryCrateSE] Begin.
2026/10/6 17:05:54: isROGConsoleProduct = 0, isDebugROGConsoleProduct = 0
2026/10/6 17:05:54: isACSE = False, isDebugACSE = False
2026/10/6 17:05:54: [isSupportArmouryCrateSE] End. (False)
```

---

## 9. 自启动与系统集成（只读查询）

### 9.1 计划任务：**未验证（权限拒绝）**

| 尝试 | 结果 |
|---|---|
| `schtasks /query /fo LIST /v /tn "\ASUS\*"` | `ERROR: The system cannot find the path specified.`（沙箱内 schtasks 不可用） |
| `schtasks /query /fo CSV /v`（全量再过滤 ASUS/Armoury/ROG） | 无输出，命令本身报错 |
| `Get-ScheduledTask` + `Get-ScheduledTaskInfo` | `拒绝访问` |
| 枚举 `C:\Windows\System32\Tasks`（PowerShell / .NET） | `EPERM` / 对路径的访问被拒绝 |

**间接证据**

| 证据 | 内容 | 推断 |
|---|---|---|
| `dll\Schedule\AsTaskSchedArg.exe` | `/change /tn "ASUS\Framework Service" /DISABLE`、`schtasks.exe` | 存在任务 **`\ASUS\Framework Service`**（高置信） |
| `dll\Schedule\RegSchdTask.exe` + `AsTaskSched.dll` | `RegisterScheduledTask`、`DeleteScheduleTask`、`Taskname: %s` | 安装期注册 / 卸载期删除该任务 |
| `dll\Tools\RegSchdTask.exe(.manifest)` | 同型第二个构建，requireAdministrator | 注册任务需提权 |
| `Process.ini[Startup]` | `\SwAgent\ArmourySwAgent.exe -s`、`\AcPowerNotification\AcPowerNotification.exe` | 伴随进程由框架自行拉起，不靠 Run 键 |

**触发条件、可执行路径、运行账户：未验证**（无法读取任务定义；`ArmourySwAgent.exe`、`AcPowerNotification.exe` 也未随本包安装，目录不存在）。

### 9.2 服务

| 服务名 | 显示名 | 启动类型 | 状态 | 帐户 | ImagePath | 说明 |
|---|---|---|---|---|---|---|
| `ArmouryCrateDownloadTool` | Armoury Crate Download Tool | `AUTO_START`(2) | `STOPPED`（`WIN32_EXIT_CODE=1077` = 从未启动） | `LocalSystem` | `C:\WINDOWS\System32\DriverStore\FileRepository\rogms.inf_amd64_fe781846488bb435\ArmouryLiveUpdate.exe` | `WIN32_SHARE_PROCESS`；同目录还有 `ArmouryUpdate.exe` 1.4 MB、`killproc.exe`、`AsZip.dll`、`ROGMS.sys` 41,464 B |
| `ROGMS` | ROG Mouse（`@oem128.inf,%ROGMS.SvcDesc%`） | `DEMAND_START`(3) | — | 内核 | `\SystemRoot\System32\DriverStore\...\ROGMS.sys` | 鼠标过滤驱动（注册表 `ArmouryFilterDriver\Mouse=1.00.61.0`） |
| `ROGKB` | ROG Keyboard | `DEMAND_START`(3) | — | 内核 | `...\ROGKB.sys` | 键盘过滤驱动（`ArmouryFilterDriver\Keyboard=1.00.43.0`） |

- `sc query ArmouryCrateService` → `FAILED 1060: The specified service does not exist`（本机未装 AC 主服务）。
- `ArmouryCrateDownloadTool` 的 exe 由 `rogms.inf` 驱动包提供，**不在 ASUS 安装目录内**。

### 9.3 注册表 Run 键（无 ASUS 项）

| 位置 | ASUS 相关项 |
|---|---|
| `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` | 无（仅 SecurityHealth、RtkAudUService） |
| `HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run` | 无（仅 360Safetray、Intel Graphics Software） |
| `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` | 该键不存在 |

→ 常驻不依赖 Run 键，由 `ArmourySocketServer.exe`（守护）+ 计划任务链驱动（推断）。

### 9.4 `HKLM\SOFTWARE\WOW6432Node\ASUS`（安装体实际写入处）

| 键 | 值 |
|---|---|
| `ArmouryDevice` | `Installation location=C:\Program Files (x86)\ASUS\ArmouryDevice`、`Version=4.2.2.8`、`Uninstall=...\{339A6383-...}\FrameworkServiceSetup.exe`、`HTTPPort=1042`、`HTTPSPort=1043` |
| `ArmouryFilterDriver` | `Mouse=1.00.61.0`、`Keyboard=1.00.43.0` |
| `ArmouryFramework\mouse` | `Version=2.3.4`、`DisplayName=Armoury Crate Plugin - Mouse`、`Uninstall=...{40fb7bea-...}\Setup.exe -uninstall` |
| `ArmouryFramework\unifyDongle` | `Version=0.1.1`、`DisplayName=Armoury Crate Plugin - UnifyDongle` |
| `ArmouryHttp\p520` | `Version=4.05.38`、`DisplayName=ROG STRIX IMPACT III WIRELESS` |
| `ArmouryHttp\6871` | `Version=4.05.38`、`PID=6871`、`FWVersion=4.00.02`、`FWDGVersion=7.00.08`、`FWBoosterVersion=0`、`DisplayName=ROG STRIX IMPACT III WIRELESS (2.4GHz)` |
| `ArmouryHttp\6873` | `DisplayName=ROG STRIX IMPACT III WIRELESS (Bluetooth)`（页面未装，见第 7 节） |
| `ArmouryHttp\6862` / `ArmouryHttp\omni` | `Version=4.02.04`、`DisplayName=ROG OMNI RECEIVER` |
| `ArmouryHttp\ModelSetupCheckedList` | `LogPath=C:\WINDOWS\system32\config\systemprofile\AppData\Local\AcSdkInsLog\ModelSetup.txt` |
| `ArmourySDK\MainSDK` | `Version=2.01.40`、`Startup=255`、`DebugServerPort=9014` |
| `ArmourySDK\MouseSDK` | `Version=3.01.48`、`SdkTypeId=1`、`SdkPath=\MouseSDK\ArmouryMsSDK.dll` |
| `ArmourySDK\UnifyDongleSDK` | `Version=1.00.10`、`SdkTypeId=21`、`SdkPath=\UnifyDongleSDK\AC_UnifyDongleSDK.dll` |
| `WebAC` | `DisableAcPowerNotification=1`、`DisableDriverInstallAC=1` |
| `WebAC\Firmware\FWUpdateTool` / `P520_Firmware` / `omni` / `p520` | 1.0.0.4 / 1.0.0.1 / 1.0.0.2 / **1.0.2.2** |
| `DeviceDriver` | （空键） |

`HKLM\SOFTWARE\ASUS`（64 位视图）只有 `ROGLiveServicePackage` → `ACInstallPath=C:\Program Files (x86)\ASUS\ArmouryDriverUpdate\AP_UPDATE\AC_loader_v33060`、`InstallFinish=1`。

> 框架写端口用的是 `HKEY_LOCAL_MACHINE\SOFTWARE\ASUS\ArmouryDevice` 路径（asar 原文），但框架是 32 位进程，实际落在 `WOW6432Node` 下——两处数值一致（1042/1043）。

### 9.5 已安装程序列表项（ASUS/ROG 相关 8 项 + HAL 2 条）

| 显示名 | 版本 | 卸载命令 / 注册键 |
|---|---|---|
| ROG 影刃 3 无线版 | 1.0.2.2 | `...\{c0bfe97f-154d-4a56-a88c-984378f29ac8}\Setup.exe -uninstall` |
| ROG STRIX IMPACT III WIRELESS (2.4GHz) | 4.05.38 | `...\{2e3ccb5f-07b8-481b-90e2-f4165a3ccd21}\Setup.exe -uninstall` |
| ROG STRIX IMPACT III WIRELESS (Bluetooth) | 4.05.38 | `...\{603cdb29-461b-42d5-98c3-58fd30e5f4df}\Setup.exe -uninstall` |
| ROG OMNI RECEIVER | 4.02.04 | `...\{c8296bac-8227-47bd-aae7-96f081d82527}\Setup.exe -uninstall` |
| ROG Omni 接收器 | 1.0.0.2 | `...\{7620362f-f7d2-46e7-bc98-7d932190450e}\Setup.exe -uninstall` |
| ASUS Framework Service | 4.2.2.8 | `...\{339A6383-7862-46DA-8A9D-E84180EF9424}\FrameworkServiceSetup.exe /uninstall` |
| ASUS Mouse HAL | 1.2.1.18 | `MsiExec.exe /I{5C648405-6B50-4AB9-8EB5-7FC289015151}` |
| ASUS Mouse HAL | 1.2.1.18 | `C:\ProgramData\Package Cache\{b67124ae-...}\AacMouseSetup.exe /uninstall` |

> 设备页/插件各有独立卸载项，说明「页面」是按产品单独安装的 InstallShield 包，与 `ArmouryHttp\<page>` 的 GUID 一一对应。

---

## 10. 进程拓扑与启动链（含 1042/1043 证据）

### 10.1 拓扑图

```text
ArmouryCrateGearInstall.exe（本次安装器，日志 PID 19668）
   └─ execute ArmourySocketServer.exe Done.
        │  C:\Program Files (x86)\ASUS\ArmouryDevice\dll\ArmourySocketServer\ArmourySocketServer.exe
        │  单实例：CreateMutex("Global\ArmourySocketServerMutex")，已在跑则退出
        │
        ├─ CreateProcessW / ShellExecuteW → "Run asus_framework"
        │     │  asus_framework.exe（requireAdministrator；日志实例 PID 5560）
        │     ├─ HTTP  127.0.0.1:1042   （express 4.21.0 + helmet + cors）
        │     ├─ HTTPS 127.0.0.1:1043   （selfsigned 运行期生成，仅内存）
        │     ├─ WebSocketServer（ws 8.17.1）
        │     │     ← ArmourySocketServer 以 ws://127.0.0.1:1042/?role=socketServer 连入
        │     ├─ checkModules() → require("...\modules\mouse")      → routers["1"]
        │     │                 → require("...\modules\unifyDongle") → routers["21"]
        │     ├─ utilityProcess.fork(".webpack/main/sdk.js",
        │     │      ["--deviceType=1", "--targetSDKPath=...\MouseSDK\ArmouryMsSDK.dll",
        │     │       "--projPath=C:\Program Files (x86)\ASUS\ArmouryDevice"])
        │     │        └─ LoadLibrary(ArmouryMsSDK.dll) → ExecuteFunction(xml, pOut)
        │     │              └─ AacMouseHal_x86.dll → HID 帧 51 31 <子命令> 00 / 65 B
        │     └─ Electron 渲染进程：View\<pid>\index.html（?isElectron=1&session=<uuid>）
        │
        └─ 看护：asus_framework is not running → 重新 Run；Stop watchdog for asus_framework
```

### 10.2 端口 1042 / 1043 证据表

| # | 证据 | 内容 |
|---|---|---|
| 1 | asar `.webpack\main\index.js`（模块 3147） | `ports:{http:"1042",https:"1043"}` —— 硬编码默认值 |
| 2 | 同文件 createServer | `o.listen(c,"127.0.0.1",...)`；成功后 `setRegistry({path:"HKEY_LOCAL_MACHINE\SOFTWARE\ASUS\ArmouryDevice", key: n?"HTTPSPort":"HTTPPort", type:"string", value: r})`，且 `isDynamicPort:!0` → **端口可动态，写出后供 SDK 读** |
| 3 | 注册表 `HKLM\SOFTWARE\WOW6432Node\ASUS\ArmouryDevice` | `HTTPPort=1042`、`HTTPSPort=1043` |
| 4 | `Framework.00.2026-10-18.log` | `00:02:40.165 ... listening on port 1042, pid is 5560`；`00:02:40.309 ... listening on port 1043, pid is 5560` |
| 5 | `MouseSDK\2026-10-18.log` | `[HttpPort]get port` → `[HttpPort]port 1042` → `[startService]port 1042`（SDK 从注册表读端口） |
| 6 | `View\6871\service.js` / `View\p520\index.js` | 默认基址 `"127.0.0.1:1042"`，可被 `--host=` 覆盖：`process.argv.find(e=>/host=/gm.test(e)).split("=")[1]` |
| 7 | `ArmourySocketServer.exe` 字符串 | `ws://127.0.0.1:`、`/?role=socketServer`、`1042` |
| 8 | asar WebSocket 处理 | `if(role==="socketServer") this.broadcastEvent({target:{role:["socketServer"]},msg:{cmd:FRAMEWORK_CHECKER}})` |
| 9 | `Service.01.2026-10-18.log` | `[Device Service] 6871 socket server connection close.` |
| 10 | `netstat -ano`（采集时） | 127.0.0.1:1042 共 68 条记录：2 条 `ESTABLISHED`（对端 58112/63196）+ 若干 `TIME_WAIT`，服务端 PID 27336；**无 1043 监听**；进程枚举被拒（`tasklist` → `Access denied`），PID 归属未验证 |

### 10.3 「ArmourySocketServer 是父、asus_framework 是子」的依据

| 证据 | 类型 | 说明 |
|---|---|---|
| `Run asus_framework`、`asus_framework is not running`、`Stop watchdog for asus_framework` | 静态字符串 | 它负责启动并 watch 框架，而非反向 |
| `CreateProcessW`、`ShellExecuteW` | 静态导入 | 具备拉起子进程能力 |
| `Global\ArmourySocketServerMutex`、`ArmourySocketServer.exe is already running` | 静态字符串 | 单实例守护模型 |
| `ArmouryCrateGearInstall\2026-10-18_00.log` | 运行期日志 | `Program.szFrameworkPath = ...\dll\ArmourySocketServer` → `execute ArmourySocketServer.exe Done.`（安装器只启动 SocketServer） |
| `dll\Process.ini` | 配置 | `[Process]` 中 SocketServer 在前、`asus_framework.exe` 在后，同属需结束的进程组 |
| asar `forkProcess` | 代码 | 框架自身只 fork `sdk.js`；反向检索 `SocketServer` 仅命中 WebBrowserEdge 与配置解析，**没有**启动 SocketServer 的调用 |

> 运行时父子 PID **未直接观测**：采集时 `tasklist` / `Get-CimInstance Win32_Process` 被沙箱拒绝（`Access denied`），且 1042 服务端 PID 27336 无法归属。上述关系为**推断（高置信）**：静态字符串 + 安装器日志 + 反向排除。

### 10.4 启动链（含触发条件）

1. **安装/升级**：`ArmouryCrateGearInstall.exe` 读 `Config.ini` → 装 vc_redist → `FrameworkServiceSetup.exe /install /quiet` → `SDK_HAL\FWMSPlugin\Setup.exe -s` → 各产品页 `Setup.exe`（GUID 一一对应）→ 最后 `execute ArmourySocketServer.exe`（8.5 样例 4）。
2. **守护**：`ArmourySocketServer.exe` 取全局互斥量；无框架则 `Run asus_framework`，随后以 `ws://127.0.0.1:1042/?role=socketServer` 连入并周期发 `FRAMEWORK_CHECKER`（`cmd:"30000"`）。
3. **框架**：`asus_framework.exe`（提权）→ `Server.construct/init` → 监听 1042/1043 → 写注册表端口 → `checkModules`（mouse/unifyDongle）→ `loadDeviceServices` → `getConnectUsbDeviceList`（`0B051ACE` + `0B051AD7`）→ `initialize Main SDK` → `SDK list count 13`。
4. **SDK 宿主**：`utilityProcess.fork(".webpack/main/sdk.js", [--deviceType, --targetSDKPath, --projPath])` → LoadLibrary `ArmouryMsSDK.dll`，把 `ExecuteFunction` 暴露成本地 HTTP 路由。
5. **页面**：渲染进程加载 `View\6871|p520\index.html`（`?isElectron=1&session=<uuid>&deviceSN=...&dongleSN=...`），用 `127.0.0.1:1042` 调 `/6871`、`/p520`；`ArmourySocketServer=true` 的页面再经 SocketServer 的 WS 通道收发事件（`socketServerEvent`）。

---

## 11. 未验证 / 待确认清单

| 项 | 状态 | 原因 / 下一步 |
|---|---|---|
| 计划任务 `\ASUS\*` 的真实清单、动作、触发器、运行账户 | **未验证** | schtasks / Get-ScheduledTask / `C:\Windows\System32\Tasks` 全部被拒（沙箱 + 非管理员）。任务名 `\ASUS\Framework Service` 仅由二进制字符串推断；需提权执行 `schtasks /query /fo LIST /v /tn "\ASUS\Framework Service"` |
| `ArmourySocketServer.exe` → `asus_framework.exe` 的实时父子 PID | **未验证（推断高置信）** | 进程枚举被拒；需提权 CIM 或 Process Monitor |
| 采集时 1042 服务端 PID 27336 的归属 | **未验证** | `tasklist /fi "PID eq 27336"` → `Access denied`；netstat 有 68 条 1042 连接但无 LISTENING 行（可能被非提权枚举隐藏） |
| `Framework\<32位hex>\` 目录名生成算法 | **未验证** | 三个 hash 并存，未定位生成代码 |
| `dexin.exe`、`MonitorDetect.exe`、`ROGWeChat.exe` 的实际用途 | **未验证** | 仅有版本资源与文件名；未反汇编（不运行 exe） |
| `ac_node_addon` 是否运行期被真正加载 | **推断** | asar `sdk.js` 有 `require("ac_node_addon")`，`.sign` 亦有同文件副本；未抓运行期加载 |
| `koffi` 81 MB 的用途 | **推断为冗余** | 不在 `package.json.dependencies`，也不在 asar node_modules；仅 unpacked 有 17 平台 .node |
| `AcLoader` 安装目录只剩日志的原因 | **推断** | 执行体在 `ProgramData\ALL\ACLoader` 与 `ArmouryDriverUpdate` 活动中 |
| `private_key.pem`（`AC_loader_v33060`）用途 | **未读取（敏感）** | 仅记录存在与大小 9.4 KB |
| 固件 `V03/V04` 两版差异与生效版本 | **部分验证** | `version.ini` 指向 `V04_00_02`、`Complete=4.00.02`，注册表与 MAFWUpdate 均为 4.00.02；V03 角色未验证 |
| `ArmouryFramework\mouse=2.3.4` 但安装包要求 2.4.3，是否功能降级 | **未验证** | 安装日志 `VersionCompare can install OOO` 显示包内 2.4.3 可装，但注册表仍为 2.3.4（本次未执行升级） |
